clc;
clear all;

% Calcula el momento de inercia de una viga de sección H
    
b_web = 250;    % Ancho del alma (web) en mm
h_web = 300;    % Altura del alma (web) en mm
b_flange = 200; % Ancho de cada brida (flange) en mm
h_flange = 20;  % Altura de cada brida (flange) en mm

    % Área y momento de inercia del alma (web)
    A_web = b_web * h_web;
    I_web = (b_web * h_web^3) / 12;
    
    % Área y momento de inercia de cada brida (flange)
    A_flange = b_flange * h_flange;
    I_flange = (b_flange * h_flange^3) / 12;
    
    % Distancia desde el centro del alma al borde de la brida
    d = (h_web / 2) + h_flange / 2;
    
    % Momento de inercia total
    I = I_web + 2 * A_flange * d^2;


disp(I/10^4)
