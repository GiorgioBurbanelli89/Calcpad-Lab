% Analisis de un portico estructura mixta plano en ladera 
%               Jorge Burbano
%% 
clc;clear %Limpieza del entorno de trabajo
% Datos del material
% Ec=150000*sqrt(210); %MÃ³dulo de elasticidad del hormigÃ³n T/m2
Secciones=3; %Definir que tipo de seccion es si es concreto=1,acero=2 y mixta con columnas CFT=3.
E=20389019.2; %kN Modulo de elasticidad de acero
beta=0;
% E=21000000
%% Porticos en sentido eje x (Eje 1-2)
sv =[2.93;4.72;3.20]; %Sepracion entre vanos
sp =[3.45;3.07]; %Altura de cada piso
Lvi=1.00; Lvd=1.00; %Longitudes de volado izquierdo y derecho
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
% CG=[0 0 0;0 0 0;1 0 2;1 0 3];ngl=3;
% [CG,ngl]=cg_sismo_gaus(nod,np,nr)%Calcula los grados de libertad
% vc Programa que calcula el vector colocación de un pórtico plano
X
Y
NI
NJ
CG
[VC]=vc(NI,NJ,CG); %Vector de conectividad
% longitud Programa que calcula la longitud de cada elemento
[L,seno,coseno]=longitud(X,Y,NI,NJ);L(2)=2;
dibujoplano(X,Y,NI,NJ)
dibujogdl_new(X,Y,NI,NJ,CG)
%% CÃ¡lculo de la matriz de rigidez del portico A,B,C y D
% Secciones de vigas metalicas 
% Secciones de columnas CFT
% Secciones de columnas huecas metalicas
ELEM=[repmat([0.50 0.50],[2,1]);...
    repmat([0.40 0.70],[1,1])];%[b,h](vigas)
% ELEM(9,1)=0.00000001;ELEM(9,2)=0.00000001
% ELEM(15,1)=ELEM(9,1);ELEM(15,2)=ELEM(9,2);
Base=ELEM(:,1);Altura=ELEM(:,2);
% Area de columnas y vigas de concreto armado
Inerciagc = zeros(size(ELEM, 1), 1);
Areagc= zeros(size(ELEM, 1), 1);
for i=1:size(ELEM, 1); %Bucle para escoger que Inercia y Area se va a usar en el programa
Areagc(i)=Base(i)*Altura(i);    
Inerciagc(i)=(Base(i)*(Altura(i))^3)/12
end
Inerciagc;
Areagc;
[A_col_acero, I_col_acero] = I_A_Col_acero(h_c_acero, b_c_acero, t_c_acero, Ec, Es, Seccion);% Area de columnas cajon huecas o rellena de concreto
% Area de columnas cajon huecas y vigas de acero seccion H
 b_c_acero=0.3 ;h_c_acero=0.3 ;t_c_acero=0.01; %b,h  columna tubular hueca 
 I_c_acero=(h_c_acero*b_c_acero)-((h_c_acero-2*t_c_acero)*(b_c_acero-2*t_c_acero));% Area de columna tubular hueca 
A_c_acero=(b_c_acero*(h_c_acero^3)/12)+((b_c_acero-2*t_c_acero)*((h_c_acero-2*t_c_acero)^3)/12);% Area de columna tubular hueca
dwv=0.20;bfv=0.10;tfv=0.0085;twv=0.0056;%dw altura, bf ancho, tf espsor del ala , tw espesor del alma
A_v_acero=(bfv*dwv)-((bfv-tfv)*(dwv-2*tfv))
I_v_acero=((bfv*dwv^3)/12)-((bfv-twv)*(dwv-2*tfv)^3)/12
Inerciaga = zeros(size(ELEM, 1), 1);
 Areaga= zeros(size(ELEM, 1), 1);
for i = 1:size(ELEM, 1) % Bucle para escoger qué Inercia y Área se va a usar en el programa
    if i <= nudcol
        Inerciaga(i) = I_c_acero;
        Areaga(i) = A_c_acero;
    else
        Inerciaga(i) = I_v_acero;   
        Areaga(i) = A_v_acero;
    end
end

Inerciaga;
Areaga;
% Area de columnas CFT y vigas de acero
Es=20389019.2;Ec=2534563.5;
b_c_acero=0.3 ;h_c_acero=0.3 ;t_c_acero=0.01; %b,h  columna tubular hueca
h_c_concreto=h_c_acero-2*t_c_acero;b_c_concreto=b_c_acero-2*t_c_acero; %h y b de concreto 
I_c_concreto=b_c_concreto*(h_c_concreto^3)/12 %Inercia del concreto relleno
A_c_concreto=h_c_concreto*b_c_concreto; %Area de concreto relleno
I_eq_acero=I_c_acero+((Ec/Es)*I_c_concreto); %Inercia equivalente
A_eq_acero=A_c_acero+((Ec/Es)*A_c_concreto); %Area equivalente
Inerciagm = zeros(size(ELEM, 1), 1);
Areagm= zeros(size(ELEM, 1), 1);
for i = 1:size(ELEM, 1) % Bucle para escoger qué Inercia y Área se va a usar en el programa
    if i <= nudcol
        Inerciagm(i) = I_eq_acero;
        Areagm(i) = A_eq_acero;
    else
        Inerciagm(i) = I_v_acero;   
        Areagm(i) = A_v_acero;
    end
end

Inerciagm;
Areagm;

Inerciagm;
Areagm;
if Secciones==1
   Inerciag = Inerciagc;
   Areag = Areagc;
elseif Secciones==2
   Inerciag = Inerciaga;
   Areag = Areaga;
else
   Inerciag = Inerciagm;
   Areag = Areagm;
end
Inerciag;Areag;
cc1=[repmat([0],[2,1]);repmat([0],[1,1])];
cc2=[repmat([0],[2,1]);repmat([0],[1,1])];
Iag=[repmat([1],[2,1]);repmat([1],[1,1])];
[KHX]=krigidez_nudo_rigido_compuesta(ngl,Areag,Inerciag,cc1,cc2,L,seno,coseno,VC,E,Iag,beta);
% Condensacion de la matriz de rigidez PORTICO A
format short 'G'
K=KHX
na=np;
kaa=KHX(1:na,1:na); kab=KHX(1:na,na+1:ngl);kba=kab';
kbb=KHX(na+1:ngl,na+1:ngl);
T=-kbb\kba;
KLA=kaa+kab*T
KLB=KLA;
% format short G
% F=[3,101.9716213,0,0];datos=0;nmc=0;Fm=0;njc=1
% % % [Q,Q2]=cargas(njc,nmc,ngl,L,seno,coseno,CG,VC,F,Fm,datos);
% % Q=[1000; 0; 0];
% % q=K\Q;
% % q(1)=q(1)*10*100;
% % q

