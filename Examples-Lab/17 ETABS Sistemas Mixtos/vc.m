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
