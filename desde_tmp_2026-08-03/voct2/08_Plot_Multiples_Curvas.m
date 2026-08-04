% Plot_Multiples_Curvas.m — Varias curvas en el mismo plot con leyenda
clear; clc;

x = linspace(-pi, pi, 200);

figure;
plot(x, sin(x), 'r-', 'LineWidth', 1.5); hold on;
plot(x, cos(x), 'b--', 'LineWidth', 1.5);
plot(x, sin(x).*cos(x), 'g:', 'LineWidth', 2);
hold off;

xlabel('x [rad]');
ylabel('y');
title('sin, cos y su producto');
legend('sin(x)', 'cos(x)', 'sin(x)*cos(x)', 'Location', 'best');
grid on;

fprintf('Tres curvas trigonometricas en el mismo grafico.\n');
