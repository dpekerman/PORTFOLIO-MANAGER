import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormField, form } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { SectorBenchmarkMapping } from '../../../../core/models/opportunities.models';
import { AuthStore } from '../../../../core/store/auth.store';
import { OpportunityReferenceStore } from '../../../../core/store/opportunity-reference.store';
import { SettingsSectionComponent } from '../../../../shared/ui/settings-section/settings-section.component';
import { benchmarkFormErrors, constituentFormErrors, parseAliases, splitAliases } from './opportunity-reference-logic';

const EMPTY_CONSTITUENT = { symbol: '', companyName: '', effectiveFrom: '', source: '', sourceAsOfDate: '' };
const EMPTY_BENCHMARK = {
  id: null as number | null,
  sector: '',
  market: 'Tsx',
  benchmarkSymbol: '',
  benchmarkName: '',
  currency: '',
  aliases: '',
  isActive: true,
  notes: '',
  source: '',
  sourceAsOfDate: '',
};

/**
 * PM-029 reference data: TSX 60 constituents and sector benchmark mappings. Nothing is seeded automatically;
 * an administrator enters each sourced, date-stamped row. Constituents are retired (EffectiveTo), never deleted.
 */
@Component({
  selector: 'app-opportunities-reference-tab',
  templateUrl: './opportunities-reference-tab.component.html',
  styleUrl: './opportunities-reference-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormField,
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatTooltipModule,
    SettingsSectionComponent,
  ],
})
export class OpportunitiesReferenceTabComponent implements OnInit {
  protected readonly store = inject(OpportunityReferenceStore);
  protected readonly isAdmin = inject(AuthStore).isAdmin;

  protected readonly constituentModel = signal({ ...EMPTY_CONSTITUENT });
  protected readonly constituentForm = form(this.constituentModel);
  protected readonly constituentErrors = computed(() => constituentFormErrors(this.constituentModel()));

  protected readonly benchmarkModel = signal({ ...EMPTY_BENCHMARK });
  protected readonly benchmarkForm = form(this.benchmarkModel);
  protected readonly benchmarkErrors = computed(() => benchmarkFormErrors(this.benchmarkModel()));

  protected readonly retireId = signal<number | null>(null);
  protected readonly showRetired = signal(false);
  protected readonly markets = ['Tsx', 'Nyse'] as const;

  ngOnInit(): void {
    this.store.load();
  }

  protected addConstituent(): void {
    if (this.constituentErrors().length || this.store.saving()) return;
    const m = this.constituentModel();
    this.store.addConstituent({
      symbol: m.symbol.trim(),
      companyName: m.companyName.trim() || null,
      indexCode: null,
      effectiveFrom: m.effectiveFrom,
      source: m.source.trim(),
      sourceAsOfDate: m.sourceAsOfDate,
    });
    // Keep the shared source fields so a sourced list can be entered row by row.
    this.constituentModel.set({ ...EMPTY_CONSTITUENT, effectiveFrom: m.effectiveFrom, source: m.source, sourceAsOfDate: m.sourceAsOfDate });
  }

  /** Two-step retire: the first click arms the row, the second confirms. */
  protected retire(id: number): void {
    if (this.retireId() !== id) {
      this.retireId.set(id);
      return;
    }
    this.retireId.set(null);
    this.store.retireConstituent({ id, effectiveTo: null });
  }

  protected editBenchmark(b: SectorBenchmarkMapping): void {
    this.benchmarkModel.set({
      id: b.id,
      sector: b.sector,
      market: b.market,
      benchmarkSymbol: b.benchmarkSymbol,
      benchmarkName: b.benchmarkName,
      currency: b.currency,
      aliases: parseAliases(b.providerSectorAliasesJson).join(', '),
      isActive: b.isActive,
      notes: b.notes ?? '',
      source: b.source ?? '',
      sourceAsOfDate: b.sourceAsOfDate?.substring(0, 10) ?? '',
    });
  }

  protected resetBenchmark(): void {
    this.benchmarkModel.set({ ...EMPTY_BENCHMARK });
  }

  protected saveBenchmark(): void {
    if (this.benchmarkErrors().length || this.store.saving()) return;
    const m = this.benchmarkModel();
    this.store.upsertBenchmark({
      id: m.id,
      sector: m.sector.trim(),
      market: m.market,
      benchmarkSymbol: m.benchmarkSymbol.trim(),
      benchmarkName: m.benchmarkName.trim() || null,
      currency: m.currency.trim(),
      providerSectorAliases: splitAliases(m.aliases),
      isActive: m.isActive,
      notes: m.notes.trim() || null,
      source: m.source.trim() || null,
      sourceAsOfDate: m.sourceAsOfDate || null,
    });
    this.resetBenchmark();
  }

  protected aliases(b: SectorBenchmarkMapping): string {
    return parseAliases(b.providerSectorAliasesJson).join(', ') || '—';
  }

  protected date(v: string | null | undefined): string {
    return v ? v.substring(0, 10) : '—';
  }
}
