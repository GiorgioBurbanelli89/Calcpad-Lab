% Título: Cálculo de inercia y área transformada de una columna CFT

% Dimensiones de la sección de acero en cm
b_ac = 20; % cm
h_ac = 20; % cm
t_ac = 0.6; % cm

% Módulos de elasticidad de los materiales en kgf/cm^2
Es = 2038902; % Módulo de acero
Ec = 219500; % Módulo de concreto

% Cálculo del área de la sección de acero (solo paredes)
A_ac = 2 * b_ac * t_ac + 2 * (h_ac - 2 * t_ac) * t_ac;
fprintf('Área de la sección de acero A_ac = %.2f cm^2\n', A_ac);

% Cálculo del área de la sección de concreto (parte rellena)
b_c = b_ac - 2 * t_ac;
h_c = h_ac - 2 * t_ac;
A_c = b_c * h_c;
fprintf('Área de la sección de concreto A_c = %.2f cm^2\n', A_c);

% Cálculo del momento de inercia de la sección de acero (aproximación a caja hueca)
I_ac = (b_ac * h_ac^3 / 12) - ((b_ac - 2 * t_ac) * (h_ac - 2 * t_ac)^3 / 12);
fprintf('Momento de inercia de la sección de acero I_ac = %.2f cm^4\n', I_ac);

% Cálculo del momento de inercia de la sección de concreto
I_c = (b_c * h_c^3) / 12;
fprintf('Momento de inercia de la sección de concreto I_c = %.2f cm^4\n', I_c);

% Relación entre los módulos de elasticidad
n = Ec / Es;
fprintf('Relación n = %.3f\n', n);

% Cálculo del momento de inercia transformada considerando la sección equivalente
I_trans = I_ac + n * I_c;
fprintf('Momento de inercia transformado I_trans = %.2f cm^4\n', I_trans);

% Cálculo del área transformada de la sección
A_trans = A_ac + n * A_c;
fprintf('Área transformada A_trans = %.2f cm^2\n', A_trans);
