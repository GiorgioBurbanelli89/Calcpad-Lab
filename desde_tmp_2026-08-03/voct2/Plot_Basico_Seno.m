% Plot_Basico_Seno.m — Plot 2D simple de una funcion
clear; clc;

x = linspace(0, 2*pi, 200);
y = sin(x);

figure;
plot(x, y, 'b-', 'LineWidth', 2);
xlabel('x [rad]');
ylabel('sin(x)');
title('Funcion sin(x) en [0, 2*pi]');
grid on;

fprintf('Plot generado: sin(x) en [0, 2*pi] con 200 puntos.\n');
