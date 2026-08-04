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