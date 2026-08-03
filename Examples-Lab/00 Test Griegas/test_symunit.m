%% symunit real de MATLAB R2017a, renderizado en Hekatan Lab
%-- Este codigo corre IGUAL en MATLAB 2017a (con Symbolic Math Toolbox) y en
%-- Hekatan Lab. Aqui las unidades se muestran en VERDE y rectas, como Calcpad.
u = symunit;

%% Geometria y cargas (unidades reales)
L = 6*u.m
b = 300*u.mm
h = 500*u.mm
q = 10*u.kN/u.m^2

%% Aritmetica con propagacion y conversion automatica de unidades
suma = 3*u.m + 200*u.cm
area = b*h
Lmm = unitConvert(L, u.mm)

%% Esfuerzo axial: sigma = F/A  (kN/m^2)
F = 250*u.kN
A = 0.2*u.m*0.3*u.m
sigma = F/A

%% Utilidades
valnum = double(L)
esUnidad = isUnit(sigma)
