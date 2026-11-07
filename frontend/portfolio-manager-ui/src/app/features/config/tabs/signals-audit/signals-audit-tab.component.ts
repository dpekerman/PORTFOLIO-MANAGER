import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { defer, map } from 'rxjs';
import { AuditedActionEntry, AuditedActionEntryView } from '../../../../core/models/portfolio.models';
import { SignalsAuditStore } from '../../../../core/store/signals-audit.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { ConfigSaveService } from '../../config-save.service';

/** Capture settings for the Signals Audit: which recommendations are recorded as history. */
@Component({
  selector: 'app-signals-audit-tab',
  templateUrl: './signals-audit-tab.component.html',
  styleUrl: './signals-audit-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatIconModule, MatInputModule, MatSlideToggleModule, SettingsSectionComponent],
})
export class SignalsAuditTabComponent implements OnInit {
  protected readonly store = inject(SignalsAuditStore);

  protected readonly enabled = signal(true);
  protected readonly threshold = signal<number | null>(75);
  protected readonly faEnabled = signal(true);
  protected readonly entries = signal<AuditedActionEntryView[]>([]);
  protected readonly filters = [
    { value: 'ALL', label: 'All' },
    { value: 'ENABLED', label: 'Enabled' },
    { value: 'DISABLED', label: 'Disabled' },
  ] as const;
  protected readonly search = signal('');
  protected readonly statusFilter = signal<'ALL' | 'ENABLED' | 'DISABLED'>('ALL');
  protected readonly newLabel = signal('');
  protected readonly editingId = signal<string | null>(null);
  protected readonly editLabel = signal('');

  protected readonly thresholdValid = computed(() => {
    const t = this.threshold();
    return t !== null && Number.isFinite(t) && t >= 0 && t <= 100;
  });

  protected readonly visibleEntries = computed(() => {
    const q = this.search().trim().toLowerCase();
    const f = this.statusFilter();
    return this.entries().filter(
      (e) =>
        (f === 'ALL' || (f === 'ENABLED') === e.enabled) && (q === '' || e.label.toLowerCase().includes(q)),
    );
  });

  protected readonly enabledCount = computed(() => this.entries().filter((e) => e.enabled).length);

  protected readonly addError = computed(() => {
    const label = this.newLabel().trim();
    if (label === '') return null;
    if (label.length > 80) return 'Keep the name under 80 characters.';
    const key = label.toUpperCase();
    return this.entries().some((e) => e.label.toUpperCase() === key || e.id === key)
      ? 'Already in the list.'
      : null;
  });

  protected readonly entriesValid = computed(
    () => this.entries().every((e) => e.label.trim().length > 0),
  );

  protected readonly dirty = computed(() => {
    const s = this.store.settings();
    if (!s) return false;
    return (
      this.enabled() !== s.priorityEnabled ||
      this.threshold() !== s.priorityThreshold ||
      this.faEnabled() !== s.finalActionEnabled ||
      JSON.stringify(this.entries().map(toPayload)) !== JSON.stringify(s.entries.map(toPayload))
    );
  });

  constructor() {
    // Seed the form from the persisted settings whenever they (re)load or are saved.
    effect(() => {
      const s = this.store.settings();
      if (!s) return;
      untracked(() => {
        this.enabled.set(s.priorityEnabled);
        this.threshold.set(s.priorityThreshold);
        this.faEnabled.set(s.finalActionEnabled);
        this.entries.set(s.entries.map((e) => ({ ...e })));
      });
    });

    inject(ConfigSaveService).register(
      {
        id: 'Signal Tracker',
        dirty: this.dirty,
        save: () =>
          defer(() => this.persist()).pipe(
            map((ok) => {
              if (!ok) throw new Error('Signal Tracker settings were not saved');
              return null;
            }),
          ),
      },
      inject(DestroyRef),
    );
  }

  ngOnInit(): void {
    this.store.loadSettings();
    this.store.loadFinalActionCatalog();
  }

  protected saveNow(): void {
    void this.persist();
  }

  protected toggleEntry(id: string, enabled: boolean): void {
    this.entries.update((list) => list.map((e) => (e.id === id ? { ...e, enabled } : e)));
  }

  protected setAll(enabled: boolean): void {
    const visible = new Set(this.visibleEntries().map((e) => e.id));
    this.entries.update((list) => list.map((e) => (visible.has(e.id) ? { ...e, enabled } : e)));
  }

  protected addEntry(): void {
    const label = this.newLabel().trim();
    if (label === '' || this.addError()) return;
    const known = this.store
      .finalActionCatalog()
      .find((c) => c.label.toUpperCase() === label.toUpperCase());
    this.entries.update((list) => [
      ...list,
      {
        id: known?.id ?? label.toUpperCase(),
        label: known?.label ?? label,
        enabled: true,
        custom: !known,
        knownToRules: !!known,
        category: known?.category ?? 'Advisory',
        direction: known?.direction ?? 'Neutral',
      } as AuditedActionEntryView,
    ]);
    this.newLabel.set('');
  }

  protected removeEntry(id: string): void {
    this.entries.update((list) => list.filter((e) => e.id !== id));
    if (this.editingId() === id) this.editingId.set(null);
  }

  protected startEdit(e: AuditedActionEntryView): void {
    this.editingId.set(e.id);
    this.editLabel.set(e.label);
  }

  protected commitEdit(): void {
    const id = this.editingId();
    const label = this.editLabel().trim();
    if (id === null) return;
    const clash = this.entries().some((e) => e.id !== id && e.label.toUpperCase() === label.toUpperCase());
    if (label !== '' && !clash) {
      this.entries.update((list) => list.map((e) => (e.id === id ? { ...e, label } : e)));
    }
    this.editingId.set(null);
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
  }

  private persist(): Promise<boolean> {
    if (!this.thresholdValid() || !this.entriesValid()) return Promise.resolve(false);
    return this.store.saveSettings(
      this.enabled(),
      this.threshold()!,
      this.faEnabled(),
      this.entries().map(toPayload),
    );
  }
}

function toPayload(e: AuditedActionEntry): AuditedActionEntry {
  return { id: e.id, label: e.label, enabled: e.enabled, custom: e.custom };
}
