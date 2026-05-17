
% Titulo: Calculo de inercia y area transformada de una columna CFT

% Dimensiones de la seccion de acero en cm (convertidas de mm a cm)
b_ac = 30; % cm
h_ac = 30; % cm
t_ac = 1;  % cm

% Modulos de elasticidad de los materiales en kgf/cm^2
Es = 2038902; % Modulo de acero
Ec = 219500;  % Modulo de concreto

% Calculo del area de la seccion de acero (solo paredes)
A_ac = 2 * b_ac * t_ac + 2 * (h_ac - 2 * t_ac) * t_ac;
fprintf('Area de la seccion de acero A_ac = %.2f cm^2\n', A_ac);

% Calculo del area de la seccion de concreto (parte rellena)
b_c = b_ac - 2 * t_ac;
h_c = h_ac - 2 * t_ac;
A_c = b_c * h_c;
fprintf('Area de la seccion de concreto A_c = %.2f cm^2\n', A_c);

% Calculo del momento de inercia de la seccion de acero (aproximacion a caja hueca)
I_ac = (b_ac * h_ac^3 / 12) - ((b_c) * (h_c)^3 / 12);
fprintf('Momento de inercia de la seccion de acero I_ac = %.2f cm^4\n', I_ac);

% Calculo del momento de inercia de la seccion de concreto
I_c = (b_c * h_c^3) / 12;
fprintf('Momento de inercia de la seccion de concreto I_c = %.2f cm^4\n', I_c);

% Relacion entre los modulos de elasticidad
n = Ec / Es;
fprintf('Relacion n = %.3f\n', n);

% Calculo del momento de inercia transformada considerando la seccion equivalente
I_trans = I_ac + n * I_c;
fprintf('Momento de inercia transformado I_trans = %.2f cm^4\n', I_trans);

% Calculo del area transformada de la seccion
A_trans = A_ac + n * A_c;
fprintf('Area transformada A_trans = %.2f cm^2\n', A_trans);
