% Inercia_Seccion_I.m — Momento de inercia de un perfil I (HEB/IPE manual)
% Computa I respecto al eje fuerte (y) usando teorema de Steiner.
clear; clc;

fprintf('=== Inercia de perfil I (calculo manual con Steiner) ===\n\n');

% Dimensiones IPE 300 (ejemplo)
h  = 300;   % altura total (mm)
b  = 150;   % ancho ala (mm)
tw = 7.1;   % espesor alma (mm)
tf = 10.7;  % espesor ala (mm)

fprintf('IPE 300:  h=%d mm, b=%d mm, tw=%g mm, tf=%g mm\n\n', h, b, tw, tf);

% Inercia respecto al eje y (eje fuerte, paralelo al ala)
% 2 alas: cada una I_propia + A*d^2
A_ala = b * tf;
d_ala = (h - tf)/2;
I_ala_propia = b * tf^3 / 12;
I_alas = 2 * (I_ala_propia + A_ala * d_ala^2);

% Alma
h_alma = h - 2*tf;
I_alma = tw * h_alma^3 / 12;

I_total = I_alas + I_alma;
fprintf('I de las dos alas (Steiner): %.4e mm^4\n', I_alas);
fprintf('I del alma:                  %.4e mm^4\n', I_alma);
fprintf('I total (eje fuerte y):      %.4e mm^4\n', I_total);
fprintf('  en m^4:                    %.4e m^4\n', I_total*1e-12);
fprintf('  tabulado IPE 300:          8.36e-05 m^4\n');
