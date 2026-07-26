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
