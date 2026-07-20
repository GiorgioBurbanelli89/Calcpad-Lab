/* hekatan-slider.js - Slider EXPLICATIVO para Calcpad (API JS).
   Coloca en el .cpd un  <div class="riemann-slider"></div>  (via #def) y esta librería lo
   convierte en un deslizador + canvas: al ARRASTRAR (evento 'input', continuo) cambia N y
   redibuja la suma de Riemann de x^2 entre 0 y 1, mostrando cómo converge a 1/3. */
(function () {
  "use strict";

  function build(div) {
    var f = function (x) { return x * x; };
    var a = 0, b = 1, exact = 1 / 3, N0 = 5;

    div.innerHTML = "";
    var canvas = document.createElement("canvas");
    canvas.width = 560; canvas.height = 300;
    canvas.style.border = "1px solid #ccc"; canvas.style.display = "block";
    var slider = document.createElement("input");
    slider.type = "range"; slider.min = 1; slider.max = 50; slider.step = 1; slider.value = N0;
    slider.style.width = "560px"; slider.style.margin = "6px 0";
    var label = document.createElement("div");
    label.style.font = "14px sans-serif";
    div.appendChild(canvas); div.appendChild(slider); div.appendChild(label);

    var ctx = canvas.getContext("2d");
    var W = canvas.width, H = canvas.height, pad = 28;
    function SX(x) { return pad + x * (W - 2 * pad); }
    function SY(y) { return (H - pad) - y * (H - 2 * pad); }

    function draw(N) {
      ctx.clearRect(0, 0, W, H);
      var h = (b - a) / N, approx = 0;
      for (var i = 1; i <= N; i++) {
        var xc = a + (i - 0.5) * h, yc = f(xc); approx += yc * h;
        var xl = a + (i - 1) * h, xr = a + i * h;
        ctx.fillStyle = "rgba(155,89,182,0.25)";
        ctx.fillRect(SX(xl), SY(yc), SX(xr) - SX(xl), SY(0) - SY(yc));
        ctx.strokeStyle = "#9b59b6";
        ctx.strokeRect(SX(xl), SY(yc), SX(xr) - SX(xl), SY(0) - SY(yc));
      }
      ctx.strokeStyle = "#1a4f8e"; ctx.lineWidth = 2; ctx.beginPath();
      for (var k = 0; k <= 200; k++) {
        var x = a + (b - a) * k / 200, X = SX(x), Y = SY(f(x));
        if (k === 0) ctx.moveTo(X, Y); else ctx.lineTo(X, Y);
      }
      ctx.stroke();
      ctx.strokeStyle = "#444"; ctx.lineWidth = 1; ctx.beginPath();
      ctx.moveTo(SX(a), SY(0)); ctx.lineTo(SX(b), SY(0)); ctx.stroke();
      var err = Math.abs(approx - exact);
      label.innerHTML = "N = <b>" + N + "</b> rect&aacute;ngulos &nbsp;&rarr;&nbsp; aprox = <b>" +
        approx.toFixed(5) + "</b> &nbsp; (exacto = " + exact.toFixed(5) +
        ", error = " + err.toFixed(5) + ")";
    }

    // CLAVE: 'input' dispara CONTINUO al ARRASTRAR (no al soltar como 'change').
    slider.addEventListener("input", function () { draw(+slider.value); });
    draw(N0);
  }

  function init() {
    var ds = document.querySelectorAll(".riemann-slider");
    for (var i = 0; i < ds.length; i++) build(ds[i]);
  }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
})();
