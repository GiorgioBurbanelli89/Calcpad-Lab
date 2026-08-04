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
ELEM=[repmat([0.50 0.50],[2,1]); repmat([0.40 0.70],[1,1])];%[b,h](vigas)
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
% [A_col_acero, I_col_acero] = I_A_Col_acero(h_c_acero, b_c_acero, t_c_acero, Ec, Es, Seccion);% Area de columnas cajon huecas o rellena de concreto
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



function [nv,np,nudt,nudcol,nudvg,nudnmc,nod,nr]=geometria_volcar(sv,sp,Lvi,Lvd)
% Programa para generar la geometría de un (***PÓRTICO REGULAR***) 
% cuando se tienen volados acartelados 
%-------------------------------------------------------------%
%               Por: PAUL ANDRES ACUÑA
%
%            Profesor: DR. ROBERTO AGUIAR
%                      ESPE
%                 Junio del 2020
%-------------------------------------------------------------%
%[nv,np,nudt,nudcol,nudvg,nudnmc,nod,nr]=geometria_volcar(sv,sp,Lvi,Lvd)
%-------------------------------------------------------------%
%
% ********** DATOS ********** %
% sv:     Vector que contiene la longitud de cada vano CENTRAL (NO VOLADOS)
% sp:     Vector que contiene la altura de cada piso
% Lvi:    Longitud del volado - lado izquierdo
% Lvd:    Longitud del volado - lado derecho
% ********* REPORTA ********* %
% nv:     Número de vanos
% np:     Número de pisos
% nudt:   Número de elementos totales
% nudcol: Número de columnas
% nudvg:  Número de vigas
% nudnmc: Número de volados (vigas) acarteladas
% nod:    Número de nudos
% nr:     Número de nudos restringidos

%Cálculos de geometría sección central (SIN VOLADOS)
nv=length(sv);      % Calcula el número de vanos
np=length(sp);      % Calcula el número de pisos
nr=nv+1;            % Calcula el número de nudos restringidos
nudcol=(np)*(nv+1); % Calcula el número de columnas
nudvg=nv*np;        % Calcula el número de vigas centrales (SIN VOLADOS)
% Cálculo del número de nudos y volados acartelados
if (Lvi==0 && Lvd~=0) || (Lvd==0 && Lvi~=0)
    nod=(nv+2)*np+nr;
    nudnmc=np;
elseif Lvi==0 && Lvd==0
    nod=(nv+1)*np+nr;
    nudnmc=0;
else
    nod=(nv+3)*(np)+nr; 
    nudnmc=2*np;
end
nudt=nudcol+nudvg+nudnmc;  % Calcula el número de elementos totales
return

function [X,Y]=glinea_portico_volcar(nv,np,sv,sp,nod,nr,Lvi,Lvd)
% Programa para generar las coordenadas X e Y de los nudos cuando 
% se tienen volados acartelados en (***PÓRTICOS REGULARES***)
%-------------------------------------------------------------%
%               Por: PAUL ANDRES ACUÑA
%
%            Profesor: DR. ROBERTO AGUIAR
%                      ESPE
%                 Junio del 2020
%-------------------------------------------------------------%
% [X,Y]=glinea_portico_volcar(nv,np,sv,sp,nod,nr,Lvi,Lvd)
%-------------------------------------------------------------%
%nv:  número de vanos
%np:  número de pisos
%sv:  vector con la longitud de cada vano en metros
%sp:  vector con la altura de pisos en metros
%nod: número de nudos
%nr:  número de restricciones
%Lvi: longitud del volado - lado izquierdo
%Lvd: longitud del volado - lado derecho

%%
% Vectores X e Y del tramo central (SIN CONSIDERAR VOLADOS)

xa=zeros(nv+1,1);
for i = 1: nv+1
    if i==1
       xa(i,1)= 0;
    else
       xa(i,1)= sv(i-1,1) +xa(i-1,1);
    end
end

ya=zeros(np+1,1);
for i = 1: np+1
    if i==1
       ya(i,1)= 0;
    else
       ya(i,1)= sp(i-1,1)+ya(i-1,1);
    end
end

nud = zeros(nod,2);
    for j=1:nr
        if j==1
            nud(j,1)= 0;
        else
            nud(j,1)= sv(j-1)+nud(j-1,1);
        end
    end
r=nr+1;

for i=1:np
    for j=1:nv+1
        nud(r,1)= xa(j,1);
        nud(r,2)= ya(i+1);
        r=r+1;
    end
end

%%
% Vectores X e Y de los VOLADOS ACARTELADOS
if Lvi==0 && Lvd==0
    X=nud(:,1)';
    Y=nud(:,2)';
    return
elseif (Lvi==0 && Lvd~=0) || (Lvd==0 && Lvi~=0)
    for i=1:np
        if Lvi==0
            nud(r,1)=xa(end,1)+Lvd;
        else
            nud(r,1)=0-Lvi;
        end
        nud(r,2)=ya(i+1);
        r=r+1;
    end
else
    for i=1:np
        for j=1:2
            if j==1
                nud(r,1)=0-Lvi;
            else
                nud(r,1)=xa(end,1)+Lvd;
            end
            nud(r,2)=ya(i+1);
            r=r+1;
        end
    end
end

X=nud(:,1)';
Y=nud(:,2)';
X=X+Lvi;
return
% ---end---

function [NI,NJ]=gn_portico_volcar(nr,nv,nudt,nudcol,nudvg,nudnmc,Lvi,Lvd)
% Programa para generar el Nudo Inicial y Final de los elementos
% en pórticos ***REGULARES** con volados 
%-------------------------------------------------------------%
%               Por: PAUL ANDRES ACUÑA
%
%            Profesor: DR. ROBERTO AGUIAR
%                      ESPE
%                 Junio del 2020
%-------------------------------------------------------------%
% [NI,NJ]=gn_portico_volcar(nr,nv,nudt,nudcol,nudvg,nudnmc,Lvi,Lvd)
%-------------------------------------------------------------
%nr:     número de restricciones
%nv:     número de vanos
%nudt:   número de elementos 
%nudcol: número de columnas 
%nudvg:  número de vigas
%nudnmc: número de volados (vigas) con cartelas
%Lvi:    longitud del volado - lado izquierdo
%Lvd:    longitud del volado - lado derecho
%NI,NJ:  Vectores con los nudos iniciales y finales generados

NI=zeros(1,nudt);
NJ=zeros(1,nudt);
%%
% Nudos inicial y final del tramo central (SIN CONSIDERAR VOLADOS)
for i=1:nudcol
  NI(1,i)=i;
  NJ(1,i)=NI(1,i)+nr;
end

p=1;
s=1;
i=1+nudcol;

for j=1:nudvg   
    NI(1,i)=(nr)*p+s;
    NJ(1,i)=NI(1,i)+1;
    i=i+1;
    s=s+1;
    if s>nv
        s=1;
        p=p+1;
    end
end
%%
% Acoplamiento de los volados
nii=NJ(i-1);
p=1;
s=1;
i=1+nudcol+nudvg;

if Lvi==0 && Lvd==0
    return
elseif (Lvi==0 && Lvd~=0) || (Lvd==0 && Lvi~=0)
    for j=1:nudnmc
        if Lvd==0
            NI(1,i)=nii+1;
            NJ(1,i)=nr*p+s;
            
        else
            NI(1,i)=nr*p+s+nv;
            NJ(1,i)=nii+1;
        end
        i=i+1;
        p=p+1;
        nii=nii+1;
    end
else
    for j=1:nudnmc/2
        NI(1,i)=nii+1;
        NJ(1,i)=(nr)*p+s;
    
        NI(1,i+1)=NJ(1,i)+nv;
        NJ(1,i+1)=NI(1,i)+1;
        i=i+2;
        p=p+1;
        nii=nii+2;
    end
end

return
% ---end---

function [CG,ngl]=cg_sismo2(nod,nr,Y)
%
% Programa para encontrar las coordenadas generalizadas
% en un Portico Plano considerando un grado de libertad por piso
% Para calcular matriz de rigidez lateral 
%
% Por: Roberto Aguiar Falconi
%           CEINCI-ESPE
%           Abril de 2012
%-------------------------------------------------------------
% [CG,ngl]=cg_sismol(nod,np,nr)
%-------------------------------------------------------------
% CG    Matriz de coordenadas generalizadas
% nod   Numero de nudos
% nr    Numero de nudos empotrados
% 
CG=zeros(nod,3);
%------------Coordenadas Principales----------------------------
[~,~,CG(:,1)]=unique(Y);%indice de valores unicos en Y
CG(:,1)=CG(:,1)-ones(nod,1);
ngl=max(CG(:,1));
%-----------Coordenadas Secundarias----------------------------
CG2=zeros(2,nod-nr);
for i=1:(nod-nr)*2
        ngl=ngl+1;
        CG2(i)=ngl;
end
CG(nr+1:size(CG,1),2:3)=CG2';
return
   

function [VC]=vc(NI,NJ,CG)
%
% Programa que calcula el vector de colocación de un pórtico plano
% O de una Armadura Plana
%
% Por: Roberto Aguiar Falconi
%           CEINCI-ESPE
%         Noviembre de 2009
%-------------------------------------------------------------
% [VC]=vc(NI,NJ,CG)
%-------------------------------------------------------------
% NI       Vector con los nudos iniciales de los elementos
% NJ       Vector con los nudos finales de los elementos
% CG       Matriz que contiene las coord. generalizadas de nudos
mbr=length(NI);icod=length(CG(1,:));VC=zeros(mbr,icod); 
for i=1:mbr
    for j=1:icod
        VC(i,j)=CG(NI(i),j);VC(i,j+icod)=CG(NJ(i),j);
    end
end
return
% ---end---


function [L,seno,coseno]=longitud (X,Y,NI,NJ)
%
% Programa que calcula longitud, seno, coseno de los elementos
%
% Por: Roberto Aguiar Falconi
%           CEINCI-ESPE
%         Septiembre de 2009
%-------------------------------------------------------------
% [L,seno,coseno]=longitud (X,Y,NI,NJ)
%-------------------------------------------------------------
% X,Y      Vector de coordenadas de los nudos
% NI,NJ    Vector de nudos inicial y final de elementos 
mbr=length(NI);
for i=1:mbr
    dx=X(NJ(i))-X(NI(i));dy=Y(NJ(i))-Y(NI(i));
    L(i)=sqrt(dx*dx+dy*dy);
    seno(i)=dy/L(i);coseno(i)=dx/L(i);
end
return
% ---end---


function dibujoplano(X,Y,NI,NJ)
%
% Programa para dibujar una estructura plana
%
% Por: Roberto Aguiar Falconi
%           CEINCI-ESPE
%         Septiembre de 2009
%-------------------------------------------------------------
% dibujo (X,Y,NI,NJ)
%-------------------------------------------------------------
% X        Vector que contiene coordenadas en X
% Y        Vector que contiene coordenadas en Y
% NI       Vector con los nudos iniciales de los elementos
% NJ       Vector con los nudos finales de los elementos

for i=1:length(NI)
    weights(i)=i;
end
set(0,'defaultfigurecolor',[1 1 1])
G =graph(NI,NJ,weights);
plot(G,'XData',X,'YData',Y,'EdgeLabel',G.Edges.Weight)
title('Numeración de nudos y elementos')

% ---end---


function dibujogdl_new(X,Y,NI,NJ,CG)
%
% Programa para dibujar una estructura plana considerando los gdl
%
% Por: LEONES OLIVES GABRIEL
%         CEINCI-ULEAM
%         JULIO 2019
% Modificado levemente por: Ing. Brian Cagua (Nov 2019)
%-------------------------------------------------------------
% dibujo (X,Y,NI,NJ,CG)
%-------------------------------------------------------------
% X        Vector que contiene coordenadas en X
% Y        Vector que contiene coordenadas en Y
% NI       Vector con los nudos iniciales de los elementos
% NJ       Vector con los nudos finales de los elementos
% CG       Matriz de coordenadas generalizadas
%%
mbr=length(NI); %número de elementos
for i=1:mbr
    dx=X(NJ(i))-X(NI(i));
    dy=Y(NJ(i))-Y(NI(i));
    L(i)=sqrt(dx*dx+dy*dy);
end
Lo=0;
for i=1:mbr
    Lo=Lo+L(i);
end
prolo=Lo/mbr;

%% Letra

tfl=6; %Tamaño de la Flecha
hfl=prolo*0.35; %Altura  de la flecha
afl=prolo*0.35; %Largo de la flecha
tlt=8; %Tamaño de la Letra
radio=prolo*0.18;% Radio de la flecha
clr='red';% Color principal de la flecha
clb='b';% Color secundario de la flecha
dfh=0.4;% Defase de la letra horizontal
dfv=0.05;% Defase de la letra vertical
dfv2=prolo*0.07;% Defase de la letra vertical 2
dfh2=tlt*0.025+(radio*1);
tmn=15; %Tamaño del nudo



%% Dibujo de la estructura
x1=min(X)-1;x2=max(X)+1;y1=min(Y)-1;y2=max(Y)+1;mbr=length(NI);
figure,title('Esquema Estructural'),xlim([x1 x2]) ,ylim([y1 y2])
if max(Y) > 3
    for i=1:mbr
        line([X(NI(i)) X(NJ(i))], [Y(NI(i)) Y(NJ(i))],'Color','k')
    end
else
    for i=1:mbr
        line([X(NI(i)) X(NJ(i))], [Y(NI(i)) Y(NJ(i))],'Color','k','LineWidth',max(Y))
    end
end
axis equal
hold on
%% Dibujo de los nudos
plot(X,Y,'.','MarkerEdgeColor',clr,'markersize',tmn)
%% Dibujo de los Grados libertad vertical
for i=1:length(X) %hasta el # de nodos=lenghth(X)
NUG(i)=i; 
end
NUG=NUG';
VG=[NUG CG(:,2)]; 
u=1;
for i=1:length(NUG)
    if CG(i,2)>0 
        NVG(u,:)=VG(i,:);
        u=u+1;
    end
end
VNG=NVG(:,1);
YV=Y(VNG);
XV=X(VNG);
for i=1:length(VNG)
ha = annotation('arrow');  % almacenamiento de la flecha en ha
ha.Parent = gca;           % asociar la flecha a los ejes actuales
ha.X = [XV(i) XV(i)];          %ubicacion en unidades de datos
ha.Y = [YV(i) YV(i)+hfl];   
ha.Color = clr;
ha.HeadWidth  = tfl;
ha.HeadLength = tfl;
end
x =XV+dfv;
y =YV+hfl/2;
a = NVG(:,2); b = num2str(a); c = cellstr(b);
h1=text(x, y, c);
set(h1,'Color',clr,'FontSize',tlt);
hold on
% obtencion del limite de abcisado vertical
for i=1:length(VNG)
hy(i,:) = [YV(i) YV(i)+hfl];   
end
maha = max(hy(:,2));
miha = min(hy(:,1));
may=max(Y);
miy=min(Y);
if maha>=may
mahy=maha;
else
mahy=may;
end
if miha>=miy
mihy=miy;
else
mihy=miha;
end



%% Dibujo de los Grados libertad horizontal
AG=[NUG CG(:,1)];
u=1;
for i=1:length(NUG)
    if CG(i,2)>0 
        NAG(u,:)=AG(i,:);
        u=u+1;
    end
end
ri=max(NAG(:,2));
for i=1:ri
    rb(i)=i;
end

RESP=histc(NAG(:,2),rb);
if RESP(1)>1 | RESP(length(rb))>1
u=1;
r=max(NAG(:,2));
for j=1:r
    for i=1:length(NAG)
            if NAG(i,2)==j
                axd2(u,:)=NAG(i,:);
             u=u+1;
             break
            end 
    end 
end
axd2;
u=1;
for i=length(axd2(:,1)):-1:1
       axd3(u,:)=axd2(i,:);
        u=u+1;
end
XAI=X(axd3(:,1));
YAI=Y(axd3(:,1));
XAD=X(axd2(:,1));
YAD=Y(axd2(:,1));
for i=1:length(axd3(:,1))
hb = annotation('arrow');  % almacenamiento de la flecha en ha
hb.Parent = gca;           % asociar la flecha a los ejes actuales
hb.X = [XAI(i)-afl XAI(i)];         % ubicacion en unidades de datos
hb.Y = [YAI(i) YAI(i)];   
hb.Color = clb;
hb.HeadWidth  = tfl;
hb.HeadLength = tfl;
end
x2 =XAI-afl;
y2 =YAI+dfv2;
a2 = axd3(:,2); b2 = num2str(a2); c2 = cellstr(b2);
h2=text(x2, y2, c2);
 set(h2,'Color',clb,'FontSize',tlt);
hold on

% obtencion del limite de abcisado horizontal
for i=1:length(axd3(:,1))
hx(i,:) = [XAI(i)-afl XAI(i)];   
end
mahb = max(hx(:,2));
mihb = min(hx(:,1));
ma=max(X);
mix=min(X);
if mahb>=ma
mahx=mahb;
else
mahx=ma;
end
if mihb>=mix
mihx=mix;
else
mihx=mihb;
end
axis([mihx-prolo*0.18 mahx+prolo*0.18 mihy-prolo*0.18 mahy+prolo*0.18])

else
    
ANG=NAG(:,1);
YA=Y(ANG);
XA=X(ANG);
for i=1:length(ANG)
hb = annotation('arrow'); % almacenamiento de la flecha en ha
hb.Parent = gca;           % asociar la flecha a los ejes actuales
hb.X = [XA(i)-afl XA(i)];          % ubicacion en unidades de datos
hb.Y = [YA(i) YA(i)];   
hb.Color = clr;
hb.HeadWidth  = tfl;
hb.HeadLength = tfl;
end
x2 =XA-afl;
y2 =YA+dfv2;
a2 = NAG(:,2); b2 = num2str(a2); c2 = cellstr(b2);
h2=text(x2, y2, c2);
set(h2,'Color',clr,'FontSize',tlt);
hold on
% obtencion del limite de abcisado horizontal
for i=1:length(ANG)
hx(i,:) = [XA(i)-afl XA(i)];   
end
mahb = max(hx(:,2));
mihb = min(hx(:,1));
ma=max(X);
mix=min(X);
if mahb>=ma
mahx=mahb;
else
mahx=ma;
end
if mihb>=mix
mihx=mix;
else
mihx=mihb;
end
axis([mihx-prolo*0.18 mahx+prolo*0.18 mihy-prolo*0.18 mahy+prolo*0.18])

end
%% Dibujo de los Grados libertad de Giro
[dim1,dim2]=size(CG);
if dim2==3
    GG=[NUG CG(:,3)];
    u=1;
for i=1:length(NUG)
    if CG(i,2)>0 
        NGG(u,:)=GG(i,:);
        u=u+1;
    end
end
GNG=NGG(:,1);
YG=Y(GNG);
XG=X(GNG);
x3 =XG-dfh2;
y3 =YG-hfl/2;
a3 = NGG(:,2); b3 = num2str(a3); c3 = cellstr(b3);
h3=text(x3, y3, c3);
set(h3,'Color',clr,'FontSize',tlt);
hold on
    for i=1:length(NGG(:,2))
% De los valores de ajuste deseados para la primera flecha
radius = radio; % Altura de arriba a abajo
centre = [XG(i) YG(i)];
arrow_angle = 240; % Ángulo de orientación deseado en grados
angle = -200; % Ángulo entre el inicio y el final de la flec
direction = 1; % para CW ingrese 1, para CCW ingrese 0
colour = clr; % Color de flecha
head_size = tfl; % Tamaño de la cabeza de flecha
    % needs hold on 
% El centro de verificación es un vector con dos puntos
[m,n] = size(centre);
if m*n ~= 2
    error('Centre must be a two element vector');
end
arrow_angle = deg2rad(arrow_angle); % Convertir ángulo a rad
angle = deg2rad(angle); % Convertir ángulo a rad
xc = centre(1);
yc = centre(2);
% Crear valores (x, y) que están en la dirección positiva a lo largo de la x
% eje y la misma altura que el centro
x_temp = centre(1) + radius;
y_temp = centre(2);
% Crear valores X y Y para los puntos de inicio y final del arco
x1 = (x_temp-xc)*cos(arrow_angle+angle/2) - ...
        (y_temp-yc)*sin(arrow_angle+angle/2) + xc;
x2 = (x_temp-xc)*cos(arrow_angle-angle/2) - ...
        (y_temp-yc)*sin(arrow_angle-angle/2) + xc;
x0 = (x_temp-xc)*cos(arrow_angle) - ...
        (y_temp-yc)*sin(arrow_angle) + xc;
y1 = (x_temp-xc)*sin(arrow_angle+angle/2) + ...
        (y_temp-yc)*cos(arrow_angle+angle/2) + yc;
y2 = (x_temp-xc)*sin(arrow_angle-angle/2) + ... 
        (y_temp-yc)*cos(arrow_angle-angle/2) + yc;
y0 = (x_temp-xc)*sin(arrow_angle) + ... 
        (y_temp-yc)*cos(arrow_angle) + yc;
% Trazar dos veces para obtener ángulos mayores de 180
i = 1;
%Creacion de puntos
P1 = struct([]);
P2 = struct([]);
P1{1} = [x1;y1]; % Point 1 - 1
P1{2} = [x2;y2]; % Point 1 - 2
P2{1} = [x0;y0]; % Point 2 - 1
P2{2} = [x0;y0]; % Point 2 - 1
centre = [xc;yc]; % centro de garantía es la dimensión correcta
n = 1000; % El número de puntos en el arco
v = struct([]);
    
while i < 3
    v1 = P1{i}-centre;
    v2 = P2{i}-centre;
    c = det([v1,v2]); % "producto cruzado" de v1 y v2
    a = linspace(0,atan2(abs(c),dot(v1,v2)),n); % Rango de ángulo
    v3 = [0,-c;c,0]*v1; % v3 se encuentra en el plano de v1 y v2 y es ortog. a v1
    v{i} = v1*cos(a)+((norm(v1)/norm(v3))*v3)*sin(a); % Arco, centro en (0,0)
    plot(v{i}(1,:)+xc,v{i}(2,:)+yc,'Color', colour) %Trazar arco, centrado en P0
    i = i + 1;
end
position = struct([]);
% Configuración de x e y para flechas CW y CCW
if direction == 1
    position{1} = [x2 y2 x2-(v{2}(1,2)+xc) y2-(v{2}(2,2)+yc)];
elseif direction == -1
    position{1} = [x1 y1 x1-(v{1}(1,2)+xc) y1-(v{1}(2,2)+yc)];
elseif direction == 2
    position{1} = [x2 y2 x2-(v{2}(1,2)+xc) y2-(v{2}(2,2)+yc)];
    position{2} = [x1 y1 x1-(v{1}(1,2)+xc) y1-(v{1}(2,2)+yc)];  
elseif direction == 0
    % no hacer nada
else
    error('direction flag not 1, -1, 2 or 0.');
end
% Bucle para cada punta de flecha
i = 1;
while i < abs(direction) + 1
    h=annotation('arrow'); %punta de flecha
    set(h,'parent', gca, 'position', position{i}, ...
        'HeadLength', head_size, 'HeadWidth', head_size,...
         'linestyle','none','Color', colour);
    i = i + 1;
end
    end
end


return
% ---end---


function [SS, kc] = krigidez_nudo_rigido_compuesta(ngl, Areag, Inerciag, cc1, cc2, L, seno, coseno, VC, E, Iag, beta,v)
    % Programa para encontrar la matriz de rigidez de un pórtico plano
    % o de una armadura plana
    %
    % Por: Roberto Aguiar Falconi
    % CEINCI-ESPE
    % Noviembre de 2011
    % Modificado por: Roberto Gilces
    % Noviembre de 2022
    %-------------------------------------------------------------
    % [SS, kc] = krigidez_nudo_rigido_compuesta()
    %-------------------------------------------------------------
    % ngl: Número de grados de libertad
    % Areag: Vector con el área de los elementos de armadura
    % Inerciag: Vector con la inercia de los elementos de pórtico plano
    % cc1: Vector con longitud del nudo inicial de cada elemento
    % cc2: Vector con longitud del nudo final de cada elemento
    % L: Vector que contiene la luz libre de los elementos
    % seno: Vector que contiene los senos de los elementos
    % coseno: Vector que contiene los cosenos de los elementos
    % VC: Matriz que contiene los vectores de colocación de elementos
    % E: Módulo de elasticidad del material
    % Iag: Factor de agrietamiento relativo
    % beta: Factor de corrección
    % SS: Matriz de rigidez de la estructura
    % kc: Celdas para almacenar las matrices de rigidez de los elementos

    % Inicialización de la matriz de rigidez global
    mbr = length(L);
    SS = zeros(ngl);
    icod = size(VC, 2);
    kc = cell(mbr, 1);  % Inicializar kc como una celda para almacenar las matrices de rigidez

    for i = 1: mbr
        if icod == 4
            Area = Areag(i);  % Área del elemento
            Lon = L(i);
            sen = seno(i);
            cose = coseno(i);
            k = kdiagonal(Area, Lon, E, sen, cose);
        else
            Lon = L(i);
            sen = seno(i);
            cose = coseno(i);
            Area = Areag(i);  % Área del elemento
            Inercia = Inerciag(i);  % Inercia gruesa del elemento
            c1 = cc1(i);  % Longitud del nudo rígido del nudo inicial
            c2 = cc2(i);  % Longitud del nudo rígido del nudo final
            fa = Iag(i);  % Factor de agrietamiento

            % Llamada a kmiembro_nudo_rigido_compuesta
            k = kmiembro_nudo_rigido_compuesta(Area, Inercia, c1, c2, Lon, E, sen, cose, beta, fa,v);
            
            % Almacenar la matriz k en una celda de kc
            kc{i} = k;
        end

        for j = 1: icod
            jj = VC(i, j);
            if jj == 0
                continue
            end
            for m = 1: icod
                mm = VC(i, m);
                if mm == 0
                    continue
                end
                SS(jj, mm) = SS(jj, mm) + k(j, m);
            end
        end
    end
    
end

function [K3] = kmiembro_nudo_rigido_compuesta(Area, Inercia, c1, c2, Lon, E, sen, cose, beta,fa,v)
% Matriz de rigidez de un elemento en coordenadas globales
%-------------------------------------------------------------
% [K3] = kmiembro_nudo_rigido_compuesta()
%-------------------------------------------------------------
% Area: Area de la sección transversal.
% Inercia: Momento de inercia de la sección transversal.
% c1: Longitud de nudo rígido del Nudo Inicial.
% c2: Longitud de nudo rígido del Nudo Final.
% Lon: Longitud del elemento.
% sen: Seno del ángulo para pasar de local a global.
% coseno: Coseno del ángulo para pasar de local a global.
% VC: Vector de colocación de elementos.
% E: Módulo de elasticidad del material.
% fa: Factor de ajuste.
% beta: Factor de corrección.

% Constantes
%v = 0.30;  % Relación de Poisson
G = E / (2 * (1 + v));  % Módulo de rigidez
Iagr = fa * Inercia;  % Inercia ajustada

% Coeficiente de rigidez
fi = (3 * E * Iagr * beta) / (G * Area * Lon^2);
kf = (4 * E * Iagr * (1 + fi)) / (Lon * (1 + 4 * fi));
a = (2 * E * Iagr * (1 - 2 * fi)) / (Lon * (1 + 4 * fi));
b = (kf + a) / Lon;
t = 2 * b / Lon;
r = E * Area / Lon;

% Matriz de rigidez en coordenadas globales
if sen == 0 % Caso de viga
    K3 = [0, 0, 0, 0, 0, 0;
          0, t, b + c1 * t, 0, -t, b + c2 * t;
          0, b + c1 * t, kf + 2 * c1 * b + c1^2 * t, 0, -(b + c1 * t), a + c1 * b + c2 * b + c1 * c2 * t;
          0, 0, 0, 0, 0, 0;
          0, -t, -(b + c1 * t), 0, t, -(b + c2 * t);
          0, b + c2 * t, a + c1 * b + c2 * b + c1 * c2 * t, 0, -(b + c2 * t), kf + 2 * c2 * b + c2^2 * t];
else % Caso general
    K3 = [t, 0, -(b + c1 * t), -t, 0, -(b + c2 * t);
          0, r, 0, 0, -r, 0;
          -(b + c1 * t), 0, kf + 2 * c1 * b + c1^2 * t, b + c1 * t, 0, a + c1 * b + c2 * b + c1 * c2 * t;
          -t, 0, b + c1 * t, t, 0, b + c2 * t;
          0, -r, 0, 0, r, 0;
          -(b + c2 * t), 0, a + c1 * b + c2 * b + c1 * c2 * t, b + c2 * t, 0, kf + 2 * c2 * b + c2^2 * t];
end
% K3=K3/1000 %ton/cm
format short G;
return
