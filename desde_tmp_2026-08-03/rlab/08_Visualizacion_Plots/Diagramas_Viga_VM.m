% Diagramas_Viga_VM.m — Diagramas de cortante V(x) y momento M(x) de una viga
clear; clc;

% Viga simplemente apoyada, carga uniforme
L = 6;       % luz [m]
q = 15e3;    % carga uniforme [N/m]
x = linspace(0, L, 100);

V = q*L/2 - q*x;
M = q*x.*(L - x)/2;

figure;
plot(x, V/1e3, 'b', 'LineWidth', 2); hold on;
plot([0 L], [0 0], 'k:');   % linea de referencia y=0
hold off;
title('Diagrama de cortante V(x)');
xlabel('x [m]'); ylabel('V [kN]');
grid on;

figure;
plot(x, M/1e3, 'r', 'LineWidth', 2); hold on;
plot([0 L], [0 0], 'k:');   % linea de referencia y=0
hold off;
title('Diagrama de momento M(x)');
xlabel('x [m]'); ylabel('M [kN*m]');
grid on;

fprintf('Diagramas V(x), M(x) para viga simplemente apoyada L=%dm, q=%g kN/m.\n', L, q/1e3);
fprintf('M_max = %.2f kN*m en x = L/2\n', max(M)/1e3);
