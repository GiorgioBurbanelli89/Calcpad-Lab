function [A, Ix, Iy] = IA_viga_acero(hw, bf, tf, tw)
    % IA_viga_acero - propiedades de una viga metalica perfil I
    %
    % Argumentos (en cm):
    %   hw : altura total del perfil
    %   bf : ancho del ala
    %   tf : espesor del ala
    %   tw : espesor del alma
    %
    % Retorna area transversal A, inercia eje fuerte Ix, eje debil Iy.

    % Area: 2 alas + 1 alma
    A = 2 * (bf * tf) + (hw - 2 * tf) * tw;

    % Inercia eje fuerte (caja externa - caja interna)
    Ix = (bf * hw^3 / 12) - ((bf - tw) * (hw - 2 * tf)^3 / 12);

    % Inercia eje debil: 2 alas (cada una bf^3/12 * tf) + alma * tw^3 / 12
    Iy = 2 * (tf * bf^3 / 12) + (hw - 2 * tf) * tw^3 / 12;
end
