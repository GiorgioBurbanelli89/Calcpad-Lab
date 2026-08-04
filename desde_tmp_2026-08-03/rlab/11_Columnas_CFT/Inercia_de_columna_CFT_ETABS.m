% Definición de las constantes y parámetros dados
Es = 2038901.92; % Modulo de elasticidad del acero en tonf/m^2
Ec = 217370.651; % Modulo de elasticidad del concreto en tonf/m^2
v_acero = 0.3; % Coeficiente de Poisson del acero

% Dimensiones de la columna de acero
bc = 25; %cm Base de la columna de acero en centímetros
hc = 25; %cm Altura de la columna de acero en centímetros
tc = 0.8; %cm Espesor de la columna de acero en centímetros

% Cálculo de las dimensiones efectivas
hconcreto = hc - 2 * tc;
bconcreto = bc - 2 * tc;

% Cálculo de la inercia y área de la sección de acero
I_acero = (bc * hc^3 - bconcreto * hconcreto^3) / 12;
A_acero = bc * hc - bconcreto * hconcreto;

% Cálculo de la inercia y área de la sección de concreto
I_concreto = (bconcreto * hconcreto^3) / 12;
A_concreto = bconcreto * hconcreto;

% Cálculo de las inercias y áreas equivalentes
I_eq_acero = I_acero + (Ec / Es) * I_concreto;
A_eq_acero = A_acero + (Ec / Es) * A_concreto;

% Definir el tipo de sección
Seccion = 'Compuesta'; % Puede ser 'Compuesta' o 'Simple'

% Selección de inercia y área según el tipo de sección
if strcmp(Seccion, 'Compuesta')
    I_seleccionada = I_eq_acero;
    A_seleccionada = A_eq_acero;
else
    I_seleccionada = I_acero;
    A_seleccionada = A_acero;
end

% Resultados
fprintf('Resultados:\n');
fprintf('Inercia del acero: %.6f cm^4\n', I_acero);
fprintf('Área del acero: %.6f cm^2\n', A_acero);
fprintf('Inercia del concreto: %.6f cm^4\n', I_concreto);
fprintf('Área del concreto: %.6f cm^2\n', A_concreto);
fprintf('Inercia equivalente del acero: %.6f cm^4\n', I_eq_acero);
fprintf('Área equivalente del acero: %.6f cm^2\n', A_eq_acero);

fprintf('Inercia seleccionada: %.6f cm^4\n', I_seleccionada);
fprintf('Área seleccionada: %.6f cm^2\n', A_seleccionada);


