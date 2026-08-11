%% Texto + variables en el render (estilo Calcpad) — prueba de todo lo nuevo
%" Formas de mezclar texto y variables en Hekatan Lab

%%
%" 1) @nombre : combinar varias variables en UNA linea visible
% Las variables van en su propia linea con comentario OCULTO (nota privada, no se ve).
a = 6;    % dimension en x  (este comentario NO se renderiza)
b = 4;    % dimension en y  (oculto)
t = 0.1;  % espesor (oculto)
q = 10;   % carga (oculto)
% ...y una linea %' visible las combina referenciandolas por nombre:
%' Slab dimensions - @a m, @b m
%' Thickness - @t m,   Load - @q kN/m²
%' Area de la losa = @{a*b} m²   (con @{expr} evalua la expresion)

%%
%" 2) @ pegado : texto ANTES / DESPUES / mezclado en la MISMA linea del codigo
% (la variable va primero en el codigo; el @ marca donde cae la ecuacion)
E = 35000     %' Modulo de elasticidad: @ MPa
nu = 0.15     %' @ es el coeficiente de Poisson
E_si = E*1000 %' Conversion a unidades consistentes: @ kN/m²

%%
%" 3) Operaciones de texto (MATLAB) que ahora soporta Hekatan Lab
nombre = strcat('Losa-','A12')      %' strcat
etiqueta = sprintf('a=%d, b=%d', a, b)  %' sprintf
val = str2double('3.1416')          %' str2double (NaN si no parsea)
ent = int2str(7.8)                  %' int2str redondea
sinO = erase('hormigon','o')        %' erase
