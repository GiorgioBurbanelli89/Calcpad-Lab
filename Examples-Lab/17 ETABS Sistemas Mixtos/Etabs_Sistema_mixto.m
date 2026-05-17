% Analisis de un portico estructura mixta plano en ladera 
%               Jorge Burbano
%% disp(sprintf('%.2f',G))
clc;clear %Limpieza del entorno de trabajo
% Datos del material
% Ec=150000*sqrt(210); %MÃ³dulo de elasticidad del hormigÃ³n T/m2
Seccion = 'Compuesta'; % Puede ser 'Compuesta' o 'Simple'
%E=20389019.2; %kN Modulo de elasticidad de acero
beta=1.2;
%E=21000000
%% Porticos en sentido eje x (Eje 2-3)
sv =[3.00;3.00]; %Sepracion entre vanos
sp =[3.00;3.00]; %Altura de cada piso
Lvi=0.00; Lvd=0.00; %Longitudes de volado izquierdo y derecho
%% Rutinas de geometria
[nv,np,nudt,nudcol,nudvg,nudnmc,nod,nr]=geometria_volcar(sv,sp,Lvi,Lvd);
[X,Y]=glinea_portico_volcar(nv,np,sv,sp,nod,nr,Lvi,Lvd);
[NI,NJ]=gn_portico_volcar(nr,nv,nudt,nudcol,nudvg,nudnmc,Lvi,Lvd); %Entrega nodo inicial y nodo final
[CG,ngl]=cg_sismo2(nod,nr,Y); %Calcula los grados de libertad
[VC]=vc(NI,NJ,CG); %Vector de conectividad
[L,seno,coseno]=longitud(X,Y,NI,NJ);
dibujoplano(X,Y,NI,NJ);
dibujogdl_new(X,Y,NI,NJ,CG);
%% CÃ¡lculo de la matriz de rigidez del portico 2-3
% Secciones de columnas CFT
% Definir los parámetros
bc = 25.00; % cm
hc = 25.00; % cm
tc = 0.8; % cm
fc=210 % kgf/cm^2
Ec = 14100*sqrt(fc); % kgf/cm^2
Es = 2038901.92; % kgf/cm^2
Seccion = 'Compuesta'; % Puede ser 'Compuesta' o 'Simple'
% Llamar a la función
[I_seleccionada, A_seleccionada] = IA_col_Acero_ETABS(bc, hc, tc, Ec, Es, Seccion);
% Secciones de vigas metalicas 
% Definir los parámetros de la viga en centímetros
hw = 20; % cm, altura del alma
bf = 10; % cm, ancho de la brida
tf = 0.85; % cm, espesor de la brida
tw = 0.56; % cm, espesor del alma
% Llamar a la función para calcular el área y los momentos de inercia
[A, Ix, Iy] = IA_viga_acero(hw, bf, tf, tw);
% Suponiendo que I_seleccionada, Ix y A ya están definidas
% Crear la matriz de inercias
Inerciag = [repmat(I_seleccionada, 6, 1); repmat(Ix, 4, 1)];
% Crear la matriz de áreas (asegúrate de que la segunda parte sea correcta)
Areag = [repmat(A_seleccionada, 6, 1); repmat(A, 4, 1)];
cc1=[repmat([0],[6,1]);repmat([0],[4,1])];
cc2=[repmat([0],[6,1]);repmat([0],[4,1])];
Iag=[repmat([1],[6,1]);repmat([1],[4,1])];
E=Es*((1)/(1000))%L Función Geometría volcar
[nv,np,nudt,nudcol,nudvg,nudnmc,nod,nr]=geometria_volcar(sv,sp,Lvi,Lvd);
[X,Y]=glinea_portico_volcar(nv,np,sv,sp,nod,nr,Lvi,Lvd);
[NI,NJ]=gn_portico_volcar(nr,nv,nudt,nudcol,nudvg,nudnmc,Lvi,Lvd); %Entrega nodo inicial y nodo final
[CG,ngl]=cg_sismo2(nod,nr,Y); %Calcula los grados de libertad
[VC]=vc(NI,NJ,CG); %Vector de conectividad
[L,seno,coseno]=longitud(X,Y,NI,NJ);
dibujoplano(X,Y,NI,NJ);
dibujogdl_new(X,Y,NI,NJ,CG);
%%
% Crear la matriz de inercias
Inerciag = [repmat(I_seleccionada, 8, 1); repmat(Ix, 6, 1)];
% Ajustar valores pequeños a un valor mínimo aceptable de 1e-10
% Crear la matriz de áreas
Areag = [repmat(A_seleccionada, 8, 1); repmat(A, 6, 1)];
% Ajustar valores pequeños a un valor mínimo aceptable de 1e-10
Areag(3) = 1e-10;
L=L*100;
Inerciag=Inerciag;Areag=Areag
disp(sprintf('%.2f',E));
%Comprobacion Coeficiente de rigidez
s_i=2; %Subindice de elemento
Area_g=Areag(1);
L_i=L(s_i);
Inercia_g=Inerciag(s_i);
I_ag=Iag(s_i);
v = 0.30  % Relación de Poisson
G = E / (2 * (1 + v));  % Módulo de rigidez
I_agr = I_ag * Inercia_g ; % Inercia ajustada
f_i = (3 * E * I_agr * beta) / (G * Area_g * L_i^2);
k_f = (4 * E * I_agr * (1 + f_i)) / (L_i * (1 + 4 * f_i));
a_i = (2 * E * I_agr * (1 - 2 * f_i)) / (L_i * (1 + 4 * f_i));
b_i = (k_f + a_i) / L_i;
t_i = 2 * b_i / L_i;
r_i = E * Area_g / L_i;
%Mostrar resultados con textos explicativos y formateo
disp(sprintf('Area_g: %.2f', Area_g));
disp(sprintf('Longitud (L_i): %.2f', L_i));
disp(sprintf('Inercia_g: %.2f', Inercia_g));
disp(sprintf('I_ag: %.2f', I_ag));
disp(sprintf('Módulo de rigidez (G): %.2f', G));
disp(sprintf('Inercia ajustada (I_agr): %.2f', I_agr));
disp(sprintf('f_i: %.2f', f_i));
disp(sprintf('k_f: %.2f', k_f));
disp(sprintf('a_i: %.2f', a_i));
disp(sprintf('b_i: %.2f', b_i));
disp(sprintf('t_i: %.2f', t_i));
disp(sprintf('r_i: %.2f', r_i));


[K,kc]=krigidez_nudo_rigido_compuesta(ngl,Areag,Inerciag,cc1,cc2,L,seno,coseno,VC,E,Iag,beta,v);
% Condensacion de la matriz de rigidez PORTICO A

format short 'G'
disp('Matriz de rigidez de columnas');
kc{1} %Matriz de rigidez de columna
disp('Matriz de rigidez de vigas');
kc{nudcol+1} %Matriz de rigidez de viga
KHX=K;
na=np;
kaa=KHX(1:na,1:na); kab=KHX(1:na,na+1:ngl);kba=kab';
kbb=KHX(na+1:ngl,na+1:ngl);
T=-kbb\kba;
KL1=kaa+kab*T
KL2=KL1;
KLB=KL2;
KLA=KLB;

