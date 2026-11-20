import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { expect, test } from './support/test';

// PM-029 rule: the design prototypes' numbers are illustrative and must never be hard-coded. Every score, sector
// comparison, exposure, price and decision comes from application data or the versioned pm029-v1 calculation.
// This guard scans production sources (not tests/fixtures) for the prototypes' distinctive values.

// Playwright runs from frontend/portfolio-manager-ui.
const ROOT = join(process.cwd(), '..', '..');
const SOURCES = [
  'frontend/portfolio-manager-ui/src/app/features/trading-opportunities',
  'frontend/portfolio-manager-ui/src/app/features/config/tabs/opportunities',
  'frontend/portfolio-manager-ui/src/app/core/store/opportunities.store.ts',
  'frontend/portfolio-manager-ui/src/app/core/store/opportunity-reference.store.ts',
  'frontend/portfolio-manager-ui/src/app/core/services/opportunities-api.service.ts',
  'frontend/portfolio-manager-ui/src/app/core/models/opportunities.models.ts',
  'backend/PortfolioManager.Api/Services/Opportunities',
  'backend/PortfolioManager.Api/Controllers/OpportunitiesController.cs',
];

const PROTOTYPE_LITERALS: { name: string; pattern: RegExp }[] = [
  { name: 'prototype ticker MDA.TO', pattern: /\bMDA\.TO\b/ },
  { name: 'prototype price 42.80', pattern: /\b42\.80?\b/ },
  { name: 'prototype sector heat +3.3 pp', pattern: /3\.3\s*pp/ },
  { name: 'prototype reward:risk 3.1×', pattern: /\b3\.1\s*[×x]/ },
  { name: 'prototype exposure 12.4 %', pattern: /\b12\.4\s*%/ },
  { name: 'prototype score rendered as template text', pattern: />\s*(91|74)\s*</ },
  { name: 'prototype score bound as a literal', pattern: /\b(score|swing|strategic)\s*[:=]\s*(91|74)\b/i },
];

const isTestFile = (path: string) => /[\\/]testing[\\/]|\.spec\.ts$|Tests?[\\/]/.test(path);

function collect(path: string, out: string[]): void {
  if (statSync(path).isDirectory()) {
    for (const entry of readdirSync(path)) collect(join(path, entry), out);
  } else if (/\.(ts|html|scss|cs)$/.test(path) && !isTestFile(path)) {
    out.push(path);
  }
}

test.describe('PM-029 no hard-coded prototype values', () => {
  test('production sources contain none of the prototype literals', async ({}, info) => {
    test.skip(info.project.name !== 'desktop-chrome', 'source scan is viewport-independent');
    const files: string[] = [];
    for (const source of SOURCES) collect(join(ROOT, source), files);
    expect(files.length, 'guard found the PM-029 sources').toBeGreaterThan(15);

    const hits: string[] = [];
    for (const file of files) {
      readFileSync(file, 'utf8')
        .split(/\r?\n/)
        .forEach((line, i) => {
          for (const { name, pattern } of PROTOTYPE_LITERALS) {
            if (pattern.test(line)) hits.push(`${relative(ROOT, file)}:${i + 1} ${name}: ${line.trim()}`);
          }
        });
    }
    expect(hits).toEqual([]);
  });
});
