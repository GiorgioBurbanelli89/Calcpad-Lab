clc
clear all
%% Porticos en sentido eje x (Eje 2-3)
sv =[3.00;3.00]; %Sepracion entre vanos
sp =[3.00;3.00]; %Altura de cada piso
f_c=210 %kgf/cm2
E=141000*sqrt(f_c);
Lvi=0.00; Lvd=0.00; %Longitudes de volado izquierdo y derecho
[nv,np,nudt,nudcol,nudvg,nudnmc,nod,nr]=geometria_volcar(sv,sp,Lvi,Lvd);
b_col=0.30;h_col=0.30;b_vig=0.30;h_vig=0.45
[B, H]=generarColumnasVigas(nudcol, nudvg, b_col, h_col, b_vig, h_vig)
[KL, X, Y, NI, NJ, CG] = rlaxinfi(B, H, sv, sp, Lvi, Lvd, E)

