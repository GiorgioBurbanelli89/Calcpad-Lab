% =====================================================================
%  Hekatan Symbolic en comentarios  %   —  notacion que MATLAB no tiene
% =====================================================================
%  Este archivo es 100% MATLAB valido: las lineas de abajo van dentro de
%  comentarios %, asi que MATLAB 2017a las IGNORA (corre sin error).
%  En HEKATAN LAB esas mismas lineas se RENDERIZAN como notacion
%  matematica (integrales, sumatorias, derivadas) con su valor.
%
%  Sintaxis de los operadores:   @ variable = inicio : fin
%  Tres modos de salida (Calcpad real), se escriben con # adelante:
%    noc -> solo la formula  ;  val -> solo el valor  ;  equ -> formula + valor
% =====================================================================

%% --- Integral definida  ($Area) ---
% #equ  A = $Area{x^2 @ x = 0 : 3}
% #val  A = $Area{x^2 @ x = 0 : 3}
% #noc  A = $Area{x^2 @ x = 0 : 3}

%% --- Sumatoria ($Sum) y Productoria ($Product) ---
% #equ  S = $Sum{k^2 @ k = 1 : 10}
% #equ  P = $Product{k @ k = 1 : 5}

%% --- Derivada en un punto ($Slope) ---
% #equ  D = $Slope{t^3 @ t = 2}

%% --- Integral DOBLE (Area anidada) ---
% #equ  V = $Area{$Area{x*y @ y = 0 : 1} @ x = 0 : 2}

%% --- Derivada PARCIAL: $Slope de una funcion de 2 variables ---
%  $Slope deriva respecto a la variable del @ y deja la otra como simbolo.
%  Eso equivale a la derivada parcial (deberia mostrar el simbolo d curvo).
% #equ  Dx = $Slope{x^2*y @ x = 2}     % parcial d/dx (x^2 y) en x=2  ->  4y

%% --- Formula simbolica, sin calcular (#noc) ---
% #noc  F = m*a
% #noc  E = m*c^2

%% --- Parte MATLAB normal (corre en ambos) ---
disp('Las lineas %  de arriba: MATLAB las ignora; Hekatan las renderiza.');
z = sqrt(3^2 + 4^2)
