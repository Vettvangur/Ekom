(function () {
  "use strict";

  function factory() {
    var chartInstances = {};

    function formatNumber(value) {
      var numericValue = Number(value || 0);

      return numericValue.toLocaleString("da-DK", {
        minimumFractionDigits: numericValue % 1 === 0 ? 0 : 2,
        maximumFractionDigits: 2
      });
    }

    function mapPoints(labels, points) {
      var lookup = {};

      (points || []).forEach(function (point) {
        lookup[point.x] = Number(point.y || 0);
      });

      return (labels || []).map(function (label) {
        return lookup[label] || 0;
      });
    }

    function destroy(chartId) {
      if (chartInstances[chartId]) {
        chartInstances[chartId].destroy();
        delete chartInstances[chartId];
      }
    }

    function buildConfig(title, labels, points, color) {
      return {
        type: "line",
        data: {
          labels: labels,
          datasets: [{
            label: title,
            data: mapPoints(labels, points),
            fill: true,
            tension: 0.25,
            borderWidth: 2,
            pointRadius: 3,
            pointHoverRadius: 5,
            borderColor: color,
            backgroundColor: "rgba(0, 123, 255, 0.12)"
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          interaction: {
            mode: "index",
            intersect: false
          },
          plugins: {
            legend: {
              display: false
            },
            tooltip: {
              callbacks: {
                label: function (context) {
                  return formatNumber(context.parsed.y);
                }
              }
            }
          },
          scales: {
            x: {
              grid: {
                display: false
              }
            },
            y: {
              beginAtZero: true,
              ticks: {
                callback: function (value) {
                  return formatNumber(value);
                }
              }
            }
          }
        }
      };
    }

    return {
      render: function (chartId, title, labels, points, color) {
        var canvas = document.getElementById(chartId);

        if (!canvas || typeof Chart === "undefined") {
          return;
        }

        destroy(chartId);

        chartInstances[chartId] = new Chart(canvas.getContext("2d"), buildConfig(title, labels || [], points || [], color));
      },
      destroy: destroy,
      destroyAll: function () {
        Object.keys(chartInstances).forEach(function (chartId) {
          destroy(chartId);
        });
      }
    };
  }

  angular.module("umbraco").factory("Ekom.Manager.ChartService", [factory]);
})();
