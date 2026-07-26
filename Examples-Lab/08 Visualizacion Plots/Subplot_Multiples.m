% Subplot_Multiples.m — Cuatro funciones trigonometricas (una figura por funcion)
clear; clc;

x = linspace(0, 4*pi, 200);

figure; plot(x, sin(x), 'r', 'LineWidth', 1.5);    title('sin(x)');    grid on;
figure; plot(x, cos(x), 'b', 'LineWidth', 1.5);    title('cos(x)');    grid on;
figure; plot(x, sin(x).^2, 'g', 'LineWidth', 1.5); title('sin^2(x)');  grid on;
yt = tan(x/4); yt(abs(yt) > 5) = NaN;   % oculta la asintota para que se vea la forma
figure; plot(x, yt, 'm', 'LineWidth', 1.5);        title('tan(x/4)');  grid on;

fprintf('Cuatro funciones trigonometricas graficadas.\n');
