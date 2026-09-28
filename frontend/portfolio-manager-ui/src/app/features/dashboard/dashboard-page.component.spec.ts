import { chartIndexForKey, nearestChartPointIndex } from './dashboard-page.component';

describe('Dashboard portfolio value chart interaction', () => {
  describe('nearestChartPointIndex', () => {
    it('maps the plot edges and midpoint to the nearest point', () => {
      const left = 100;
      const width = 480;

      expect(nearestChartPointIndex(left + 33, left, width, 5)).toBe(0);
      expect(nearestChartPointIndex(left + 257, left, width, 5)).toBe(2);
      expect(nearestChartPointIndex(left + 478, left, width, 5)).toBe(4);
    });

    it('uses the rendered SVG width when mapping responsive coordinates', () => {
      expect(nearestChartPointIndex(240, 0, 480, 11)).toBe(5);
      expect(nearestChartPointIndex(120, 0, 240, 11)).toBe(5);
    });

    it('clamps pointer positions outside the plot', () => {
      expect(nearestChartPointIndex(-100, 0, 960, 10)).toBe(0);
      expect(nearestChartPointIndex(1200, 0, 960, 10)).toBe(9);
    });

    it('handles empty, single-point, and invalid bounds', () => {
      expect(nearestChartPointIndex(100, 0, 960, 0)).toBeNull();
      expect(nearestChartPointIndex(100, 0, 960, 1)).toBe(0);
      expect(nearestChartPointIndex(100, 0, 0, 5)).toBeNull();
      expect(nearestChartPointIndex(Number.NaN, 0, 960, 5)).toBeNull();
    });
  });

  describe('chartIndexForKey', () => {
    it('starts at the appropriate edge and stays within bounds', () => {
      expect(chartIndexForKey('ArrowRight', null, 5)).toBe(0);
      expect(chartIndexForKey('ArrowLeft', null, 5)).toBe(4);
      expect(chartIndexForKey('ArrowLeft', 0, 5)).toBe(0);
      expect(chartIndexForKey('ArrowRight', 4, 5)).toBe(4);
    });

    it('supports direct edge navigation and clearing', () => {
      expect(chartIndexForKey('Home', 3, 5)).toBe(0);
      expect(chartIndexForKey('End', 1, 5)).toBe(4);
      expect(chartIndexForKey('Escape', 2, 5)).toBeNull();
      expect(chartIndexForKey('ArrowRight', null, 0)).toBeNull();
    });

    it('ignores unrelated keys', () => {
      expect(chartIndexForKey('Enter', 2, 5)).toBe(2);
    });
  });
});
