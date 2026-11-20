import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { OpportunitySectorHeat } from '../../../core/models/opportunities.models';
import { DemoModeStore } from '../../../core/store/demo-mode.store';
import { sectorHeatAriaLabel, sectorHeatClass, sectorHeatText, signedPp } from '../opportunities-logic';

/** Signed relative-return text; the badge background independently represents absolute benchmark momentum. */
@Component({
  selector: 'app-sector-heat-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTooltipModule],
  templateUrl: './sector-heat-badge.component.html',
  styleUrl: './sector-heat-badge.component.scss',
})
export class SectorHeatBadgeComponent {
  private readonly demo = inject(DemoModeStore);
  readonly heat = input.required<OpportunitySectorHeat>();
  readonly sector = input<string | null>(null);

  protected readonly cls = computed(() => sectorHeatClass(this.heat()));
  protected readonly text = computed(() => sectorHeatText(this.heat(), (v) => this.demo.maskPercent(v)));
  protected readonly label = computed(() => sectorHeatAriaLabel(this.heat(), this.sector()));
  protected readonly tooltip = computed(() => {
    const h = this.heat();
    if (h.diffPp === null || h.status !== 'Matched') return this.label();
    const pct = (v: number | null) => (v === null ? 'n/a' : `${signedPp(this.demo.maskPercent(v))}%`);
    const d60 = h.diff60Pp === null ? 'n/a' : `${signedPp(this.demo.maskPercent(h.diff60Pp))} pp`;
    const momentum =
      h.benchmarkReturnPct === null ? 'unavailable' : h.benchmarkReturnPct > 0 ? 'strong' : h.benchmarkReturnPct < 0 ? 'weak' : 'neutral';
    return `20 sessions: stock ${pct(h.stockReturnPct)} vs ${h.benchmarkSymbol ?? 'benchmark'} ${pct(h.benchmarkReturnPct)} · benchmark momentum ${momentum} · 60 sessions: ${d60}`;
  });
}
