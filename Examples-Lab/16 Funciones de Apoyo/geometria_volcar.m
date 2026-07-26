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