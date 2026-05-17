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
   