import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatTooltipModule } from '@angular/material/tooltip';
import { OpportunityScoreSummary, ScorePerspective } from '../../../core/models/opportunities.models';
import { heatClass, reasonLabel, scoreAriaLabel, scoreText } from '../opportunities-logic';

/** Fully coloured score cell. Provisional scores are hatched and marked "P"; unscorable shows N/A with its reason. */
@Component({
  selector: 'app-score-heat-cell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTooltipModule],
  templateUrl: './score-heat-cell.component.html',
  styleUrl: './score-heat-cell.component.scss',
})
export class ScoreHeatCellComponent {
  readonly summary = input.required<OpportunityScoreSummary>();
  readonly perspective = input.required<ScorePerspective>();

  protected readonly cls = computed(() => heatClass(this.summary()));
  protected readonly text = computed(() => scoreText(this.summary()));
  protected readonly label = computed(() => scoreAriaLabel(this.perspective(), this.summary()));
  protected readonly provisional = computed(() => {
    const s = this.summary();
    return s.status === 'Provisional' && s.score !== null;
  });
  protected readonly tooltip = computed(() => {
    const s = this.summary();
    const head =
      s.score === null
        ? `${this.perspective()}: not scorable (${s.status})`
        : `${this.perspective()}: ${s.band}, ${s.status}`;
    const reasons = s.reasonCodes.map(reasonLabel).join('; ');
    return `${head} · evidence ${s.completenessPct}% complete${reasons ? ` · ${reasons}` : ''}`;
  });
}
