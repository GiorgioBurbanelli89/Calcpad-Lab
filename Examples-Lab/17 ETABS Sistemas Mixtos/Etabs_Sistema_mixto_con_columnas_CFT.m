% Analisis de un portico estructura mixta plano en ladera 

clc;
clear all; % Limpieza del entorno de trabajo

% Datos del material
% Ec = 150000 * sqrt(210); % Módulo de elasticidad del hormigón en T/m2
Seccion = 'Compuesta'; % Puede ser 'Compuesta' o 'Simple'
% E = 20389019.2; % kN Modulo de elasticidad del acero
beta = 1.2; %Factor de forma de corte
% E = 21000000;

%% Pórticos en sentido eje x (Eje 2-3)
sv = [4.00]; % Separación entre vanos
sp = [3.00;3.50]; % Altura de cada piso
Lvi = 1.00; % Longitudes de volado izquierdo 
Lvd = 2.00; % Longitudes de volado derecho

%% Rutinas de geometría
% Geometria_volcar determina datos de geometría de un pórtico
% plano regular para el análisis de KL.
[nv, np, nudt, nudcol, nudvg, nudnmc, nod, nr] = geometria_volcar(sv, sp, Lvi, Lvd);

% La función glinea_portico_volcar determina los dos vectores X, Y 
% con las coordenadas de los nudos a partir de los resultados 
% que reporta el programa anterior. Sirve solo para pórticos regulares, considerando nudo en la mitad de las vigas.
[X, Y] = glinea_portico_volcar(nv, np, sv, sp, nod, nr, Lvi, Lvd);

% Programa para generar nodo inicial y final de los elementos del pórtico
[NI, NJ] = gn_portico_volcar(nr, nv, nudt, nudcol, nudvg, nudnmc, Lvi, Lvd);

% Considera por piso un solo grado de libertad lateral 
[CG, ngl] = cg_sismo2(nod, nr, Y); % Calcula los grados de libertad

% Programa que calcula el vector de conectividad de un pórtico plano
[VC] = vc(NI, NJ, CG); % Vector de conectividad

% Programa que calcula la longitud de cada elemento
[L, seno, coseno] = longitud(X, Y, NI, NJ);

% Dibujar el pórtico y grados de libertad
dibujoplano(X, Y, NI, NJ);
dibujogdl_new(X, Y, NI, NJ, CG);

%% Cálculo de la matriz de rigidez del pórtico 2-3

% Secciones de columnas CFT
% Definir los parámetros
bc = 20.00; % cm
hc = 20.00; % cm
tc = 0.6; % cm
fc = 210; % kgf/cm^2
Ec = 14100 * sqrt(fc); % kgf/cm^2
Es = 2038901.92; % kgf/cm^2

Seccion = 'Compuesta'; % Puede ser 'Compuesta' o 'Simple'

% Llamar a la función
[I_seleccionada, A_seleccionada] = IA_col_Acero_ETABS(bc, hc, tc, Ec, Es, Seccion);

% Secciones de vigas metálicas 
% Definir los parámetros de la viga en centímetros
hw = 30; % cm, altura del alma
bf = 15; % cm, ancho de la brida
tf = 1.0; % cm, espesor del ala
tw = 0.8; % cm, espesor del alma

% Llamar a la función para calcular el área y los momentos de inercia
[A, Ix, Iy] = IA_viga_acero(hw, bf, tf, tw);

% Crear la matriz de inercias
Inerciag = [repmat(I_seleccionada, nudcol, 1); repmat(Ix, nudvg, 1)];

% Crear la matriz de áreas (asegúrate de que la segunda parte sea correcta)
Areag = [repmat(A_seleccionada, nudcol, 1); repmat(A, nudvg, 1)];

% Vectores adicionales
cc1 = [repmat(0, [nudcol, 1]); repmat(0, [nudvg, 1])];
cc2 = [repmat(0, [nudcol, 1]); repmat(0, [nudvg, 1])];
Iag = [repmat(1, [nudcol, 1]); repmat(1, [nudvg, 1])];

% Cálculo del módulo de elasticidad
% L = 500; % Definir L si es necesario
E = Es * (1 / 1000) * L; % Ajuste en la expresión para evitar errores de sintaxis

%%
% Crear la matriz de inercias
Inerciag = [repmat(I_seleccionada, nudcol, 1); repmat(Ix, nudvg, 1)];
% Crear la matriz de áreas
Areag = [repmat(A_seleccionada, nudcol, 1); repmat(A, nudvg, 1)];
% Ajustar valores pequeños a un valor mínimo aceptable de 1e-10
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
KL2=KL1
% format short G
% F=[3,101.9716213,0,0];datos=0;nmc=0;Fm=0;njc=1
% % % [Q,Q2]=cargas(njc,nmc,ngl,L,seno,coseno,CG,VC,F,Fm,datos);
% % Q=[1000; 0; 0];
% % q=K\Q;
% % q(1)=q(1)*10*100;
% % q
%% Portico eje 1 direccion x
%% Porticos en sentido eje x (Eje 2-3)
sv =[2.93;4.72;3.20]; %Sepracion entre vanos
sp =[3.45;3.07]; %Altura de cada piso
Lvi=0.00; Lvd=0.00; %Longitudes de volado izquierdo y derecho
%% Rutinas de geometria
%Geometria_volcar determina datos de geometría de un pórtico
% plano regular para el análisis de K
Areag(4) = 1e-10;
Areag(7) = 1e-10;
Areag(8) = 1e-10;
Areag(10) = 1e-10;
Areag(11) = 1e-10;
Areag(13) = 1e-10;
Areag(14) = 1e-10;
%Comprobacion Coeficiente de rigidez
s_i=1; %Subindice de elemento
Area_g=Areag(1);
L_i=L(s_i);
Inercia_g=Inerciag(s_i);
I_ag=Iag(s_i);
v = 0.30  % Relación de Poisson
G = E / (2 * (1 + v));  % Módulo de rigidez
I_agr = I_ag * Inercia_g;  % Inercia ajustada
f_i = (3 * E * I_agr * beta) / (G * Area_g * L_i^2);
k_f = (4 * E * I_agr * (1 + f_i)) / (L_i * (1 + 4 * f_i));
a_i = (2 * E * I_agr * (1 - 2 * f_i)) / (L_i * (1 + 4 * f_i));
b_i = (k_f + a_i) / L_i;
t_i = 2 * b_i / L_i;
r_i = E * Area_g / L_i;
% E=Es*((100^2)/(1000))
% L=L;
% Inerciag=Inerciag/100^4;Areag=Areag/100^2
% disp(sprintf('%.2f',E));
% Mostrar resultados con textos explicativos y formateo
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
L=L*100;
[K,kc]=krigidez_nudo_rigido_compuesta(ngl,Areag,Inerciag,cc1,cc2,L,seno,coseno,VC,E,Iag,beta,v);
% Condensacion de la matriz de rigidez PORTICO A
format short 'G'
disp('Matriz de rigidez de columnas');
kc{1} %Matriz de rigidez de columna
disp('Matriz de rigidez de columnas');
kc{nudcol+1} %Matriz de rigidez de viga
KHX=K;
na=np;
kaa=KHX(1:na,1:na); kab=KHX(1:na,na+1:ngl);kba=kab';
kbb=KHX(na+1:ngl,na+1:ngl);
T=-kbb\kba;
KL3=kaa+kab*T
KLX=[KL1;KL2;KL3];
%% Porticos en sentido eje y (Eje 1-2)
clc
sv =[3.44;4.02]; %Sepracion entre vanos
sp =[3.45;3.07]; %Altura de cada piso
Lvi=0.00; Lvd=0.00; %Longitudes de volado izquierdo y derecho
%% Rutinas de geometria
%Geometria_volcar determina datos de geometría de un pórtico
% plano regular para el análisis de KL Función Geometría volcar
[nv,np,nudt,nudcol,nudvg,nudnmc,nod,nr]=geometria_volcar(sv,sp,Lvi,Lvd);
%La funcion glinea_portico_volcar determina los dos vectores X, Y 
%con las coordenadas de los nudos a partir de los resultados 
%que reporta el programa anterior. Solo sirve para pórticos regulares, considerando nudo en la mitad de las vigas.
[X,Y]=glinea_portico_volcar(nv,np,sv,sp,nod,nr,Lvi,Lvd);
%gn_portico_volcar programa para generar nudo Inicial y Final de los elementos del portico
[NI,NJ]=gn_portico_volcar(nr,nv,nudt,nudcol,nudvg,nudnmc,Lvi,Lvd); %Entrega nodo inicial y nodo final
%cg_sismo2   Considera por piso un solo grado de libertad lateral 
[CG,ngl]=cg_sismo2(nod,nr,Y); %Calcula los grados de libertad
% vc Programa que calcula el vector colocación de un pórtico plano
[VC]=vc(NI,NJ,CG); %Vector de conectividad
% longitud Programa que calcula la longitud de cada elemento
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

%Comprobacion Coeficiente de rigidez
s_i=1; %Subindice de elemento
Area_g=Areag(1);
L_i=L(s_i);
Inercia_g=Inerciag(s_i);
I_ag=Iag(s_i);
v = 0.30  % Relación de Poisson
G = E / (2 * (1 + v));  % Módulo de rigidez
I_agr = I_ag * Inercia_g;  % Inercia ajustada
f_i = (3 * E * I_agr * beta) / (G * Area_g * L_i^2);
k_f = (4 * E * I_agr * (1 + f_i)) / (L_i * (1 + 4 * f_i));
a_i = (2 * E * I_agr * (1 - 2 * f_i)) / (L_i * (1 + 4 * f_i));
b_i = (k_f + a_i) / L_i;
t_i = 2 * b_i / L_i;
r_i = E * Area_g / L_i;
% E=Es*((100^2)/(1000))
% L=L;
% Inerciag=Inerciag/100^4;Areag=Areag/100^2
% disp(sprintf('%.2f',E));
% Mostrar resultados con textos explicativos y formateo
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
L=L*100;
[K,kc]=krigidez_nudo_rigido_compuesta(ngl,Areag,Inerciag,cc1,cc2,L,seno,coseno,VC,E,Iag,beta,v);
% Condensacion de la matriz de rigidez PORTICO A
format short 'G'
disp('Matriz de rigidez de columnas');
kc{1} %Matriz de rigidez de columna
disp('Matriz de rigidez de columnas');
kc{nudcol+1} %Matriz de rigidez de viga
KHX=K;
na=np;
kaa=KHX(1:na,1:na); kab=KHX(1:na,na+1:ngl);kba=kab';
kbb=KHX(na+1:ngl,na+1:ngl);
T=-kbb\kba;
KL3=kaa+kab*T
KLX=[KL1;KL2;KL3];



