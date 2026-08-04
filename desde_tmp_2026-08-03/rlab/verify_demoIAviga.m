% Demo de IA_viga_acero - perfil IPE 160
% hw = 16 cm   (altura total)
% bf = 8.2 cm  (ancho de ala)
% tf = 0.74 cm (espesor de ala)
% tw = 0.5 cm  (espesor de alma)

hw_demo = 16;
bf_demo = 8.2;
tf_demo = 0.74;
tw_demo = 0.5;

[A, Ix, Iy] = IA_viga_acero(hw_demo, bf_demo, tf_demo, tw_demo);

% --- Texto plano via fprintf ---
fprintf('--- Propiedades viga IPE 160 ---\n');
fprintf('  A  = %.3f cm^2\n', A);
fprintf('  Ix = %.2f cm^4   (eje fuerte)\n', Ix);
fprintf('  Iy = %.2f cm^4   (eje debil)\n', Iy);

% --- Echo matematico ---
A
Ix
Iy
r_x = sqrt(Ix/A)
r_y = sqrt(Iy/A)


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
