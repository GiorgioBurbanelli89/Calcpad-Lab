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
