% Subplot_Multiples.m — Multiples subplots en una figura
clear; clc;

x = linspace(0, 4*pi, 200);

figure;
subplot(2,2,1); plot(x, sin(x), 'r');    title('sin(x)');    grid on;
subplot(2,2,2); plot(x, cos(x), 'b');    title('cos(x)');    grid on;
subplot(2,2,3); plot(x, sin(x).^2, 'g'); title('sin^2(x)');  grid on;
subplot(2,2,4); plot(x, tan(x/4), 'm');  title('tan(x/4)');  grid on;
ylim([-5 5]);

sgtitle('Cuatro funciones trigonometricas');

fprintf('Subplot 2x2 con 4 funciones trigonometricas.\n');
