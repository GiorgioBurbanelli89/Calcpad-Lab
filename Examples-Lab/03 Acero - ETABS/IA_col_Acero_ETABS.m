function [I_seleccionada, A_seleccionada] = IA_col_Acero_ETABS(bc, hc, tc, Ec, Es, Seccion)
    % IA_col_Acero_ETABS - propiedades de columna CFT (Concrete-Filled Tube)
    %
    % Argumentos:
    %   bc, hc : base y altura externa de la columna de acero  [m]
    %   tc     : espesor de la pared del tubo de acero         [m]
    %   Ec     : modulo elastico del concreto                  [tonf/m^2]
    %   Es     : modulo elastico del acero                     [tonf/m^2]
    %   Seccion: 'Compuesta' (transformada con n=Ec/Es) o 'Simple' (solo acero)
    %
    % Retorna inercia y area de la seccion seleccionada.

    % Dimensiones del nucleo de concreto
    hconcreto = hc - 2 * tc;
    bconcreto = bc - 2 * tc;

    % Inercia y area del acero (caja hueca)
    I_acero = ((bc * hc^3) - (bconcreto * hconcreto^3)) / 12;
    A_acero = (bc * hc) - bconcreto * hconcreto;

    % Inercia y area del concreto (parte rellena)
    I_concreto = (bconcreto * hconcreto^3) / 12;
    A_concreto = bconcreto * hconcreto;

    % Seccion transformada: equivalente a acero usando n = Ec/Es
    I_eq_acero = I_acero + (Ec / Es) * I_concreto;
    A_eq_acero = A_acero + (Ec / Es) * A_concreto;

    if strcmp(Seccion, 'Compuesta')
        I_seleccionada = I_eq_acero;
        A_seleccionada = A_eq_acero;
    else
        I_seleccionada = I_acero;
        A_seleccionada = A_acero;
    end
end
