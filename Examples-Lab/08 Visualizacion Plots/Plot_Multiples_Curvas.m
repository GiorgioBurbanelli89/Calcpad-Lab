% Plot_Multiples_Curvas.m — Varias curvas en el mismo plot con leyenda
clear; clc;

x = linspace(-pi, pi, 200);

figure;
plot(x, sin(x), 'r-', 'LineWidth', 1.5, 'DisplayName', 'sin(x)'); hold on;
plot(x, cos(x), 'b--', 'LineWidth', 1.5, 'DisplayName', 'cos(x)');
plot(x, sin(x).*cos(x), 'g:', 'LineWidth', 2, 'DisplayName', 'sin(x)*cos(x)');
hold off;

xlabel('x [rad]');
ylabel('y');
title('sin, cos y su producto');
legend('Location', 'northeast');
grid on;

fprintf('Tres curvas trigonometricas en el mismo grafico.\n');
