% ═══ PROGRAMA INTERACTIVO (Piso 3) ═══════════════════════════════
%  Mueve el SLIDER, cambia el numero de lados o el checkbox:
%  la salida y el grafico se RECALCULAN EN VIVO en el WebView2.
%  slider/numbox/checkbox emiten controles HTML y devuelven su valor;
%  al cambiarlos, Hekatan re-ejecuta el script con el nuevo valor.
% ══════════════════════════════════════════════════════════════════

r       = slider('Radio r (m)', 3, 1, 10);
n       = numbox('Lados del poligono inscrito', 6);
mostrar = checkbox('Mostrar poligono', 1);

A = pi * r^2;
fprintf('Radio = %.2f m   ->   Area del circulo = %.4f m^2\n', r, A);

th = linspace(0, 2*pi, 200);
figure; hold on; axis equal; grid on;
plot(r*cos(th), r*sin(th), 'b', 'LineWidth', 2);

if mostrar
    k = 0:n;   ang = 2*pi*k/n;
    plot(r*cos(ang), r*sin(ang), 'r-', 'LineWidth', 1.5);
    Apol = 0.5 * n * r^2 * sin(2*pi/n);
    fprintf('Poligono de %d lados: area = %.4f m^2  (%.1f%% del circulo)\n', ...
            n, Apol, 100*Apol/A);
end
title(sprintf('Radio %.2f m  —  Area circulo = %.3f m^2', r, A));
xlabel('x (m)');  ylabel('y (m)');
