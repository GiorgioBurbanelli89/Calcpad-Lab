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