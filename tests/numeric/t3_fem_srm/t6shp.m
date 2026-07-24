function [Nv,dNdL1,dNdL2]=t6shp(L1,L2)
% Funciones de forma T6 (triangulo cuadratico). Nodos medios: 4=(1-2), 5=(2-3), 6=(3-1).
L3=1-L1-L2;
Nv=[L1*(2*L1-1); L2*(2*L2-1); L3*(2*L3-1); 4*L1*L2; 4*L2*L3; 4*L3*L1];
dNdL1=[4*L1-1;0;-(4*L3-1);4*L2;-4*L2;4*L3-4*L1];
dNdL2=[0;4*L2-1;-(4*L3-1);4*L1;4*L3-4*L2;-4*L1];
