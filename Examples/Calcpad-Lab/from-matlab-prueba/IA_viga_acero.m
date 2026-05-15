function [A, Ix, Iy] = IA_viga_acero(hw, bf, tf, tw)
    % Función para calcular las propiedades de una viga metálica
    % Argumentos de entrada:
    % hw: Altura del alma de la viga en cm
    % bf: Ancho de la brida de la viga en cm
    % tf: Espesor de la brida de la viga en cm
    % tw: Espesor del alma de la viga en cm

    % Cálculo del área de la sección transversal de la viga
    A = 2 * (bf * tf) + (hw - 2 * tf) * tw;

    % Cálculo del momento de inercia alrededor del eje x (Ix)
    Ix = (bf * hw^3 / 12)- ((bf - tw) * (hw - 2 * tf)^3 / 12);
 
    % Cálculo del momento de inercia alrededor del eje y (Iy)
    Iy = 2 * (tf * bf^3 / 12) + (hw - 2 * tf) * tw^3 / 12;
end
