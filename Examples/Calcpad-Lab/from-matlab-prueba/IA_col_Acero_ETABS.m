function [I_seleccionada, A_seleccionada] = IA_col_Acero_ETABS(bc, hc, tc, Ec, Es, Seccion)
    % Función para calcular las propiedades de una columna CFT
    % Argumentos de entrada:
    % bc: Base de la columna de acero en m
    % hc: Altura de la columna de acero en m
    % tc: Espesor de la columna de acero en m
    % Ec: Módulo de elasticidad del concreto en tonf/m^2
    % Es: Módulo de elasticidad del acero en tonf/m^2
    % Seccion: Tipo de sección ('Compuesta' o 'Simple')

    % Cálculo de las dimensiones efectivas
    hconcreto = hc - 2 * tc;
    bconcreto = bc - 2 * tc;

    % Cálculo de la inercia y área de la sección de acero
    I_acero = ((bc * hc^3) - (bconcreto * hconcreto^3)) / 12;
    A_acero = (bc * hc) - bconcreto * hconcreto;

    % Cálculo de la inercia y área de la sección de concreto
    I_concreto = (bconcreto * hconcreto^3) / 12;
    A_concreto = bconcreto * hconcreto;

    % Cálculo de las inercias y áreas equivalentes
    I_eq_acero = I_acero + (Ec / Es) * I_concreto;
    A_eq_acero = A_acero + (Ec / Es) * A_concreto;

    % Selección de inercia y área según el tipo de sección
    if strcmp(Seccion, 'Compuesta')
        I_seleccionada = I_eq_acero;
        A_seleccionada = A_eq_acero;
    else
        I_seleccionada = I_acero;
        A_seleccionada = A_acero;
    end
end
