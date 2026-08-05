function Interactivo_Circulo
% ═══ PROGRAMA INTERACTIVO — uicontrol NATIVO de MATLAB ════════════
%  El MISMO .m corre en MATLAB 2017a y en Hekatan Lab. Mueve el slider
%  "Radio" y el circulo + area se recalculan EN VIVO:
%    · MATLAB : el Callback del slider redibuja (slider y circulo en la
%               MISMA figura).
%    · Hekatan: al mover el slider (WebView2) se re-ejecuta el programa y
%               get(h,'Value') devuelve el valor vivo.
% ══════════════════════════════════════════════════════════════════
f  = figure('Name','Circulo interactivo','Color','w');
ax = axes('Parent', f, 'Position', [0.13 0.24 0.80 0.66]);
h  = uicontrol('Style','slider', 'Tag','Radio', 'Min',1, 'Max',10, 'Value',3, ...
               'Units','normalized', 'Position',[0.13 0.07 0.80 0.05], ...
               'Callback', @(s,~) dibujar(ax, get(s,'Value')));
% MATLAB: el 'Callback' del slider dispara solo AL SOLTAR. Para actualizar
% MIENTRAS se desliza (en vivo), MATLAB usa un listener de cambio continuo.
% Hekatan lo ignora (su vivo viene del 'input' del control en el WebView2).
addlistener(h, 'ContinuousValueChange', @(s,~) dibujar(ax, get(s,'Value')));
dibujar(ax, get(h,'Value'));    % lee el valor (MATLAB: 3 inicial; Hekatan: valor vivo)
end

function dibujar(ax, r)
A  = pi * r^2;
th = linspace(0, 2*pi, 200);
plot(ax, r*cos(th), r*sin(th), 'b', 'LineWidth', 2);
axis(ax, 'equal');   grid(ax, 'on');
title(ax, sprintf('Radio %.2f m  —  Area = %.3f m^2', r, A));
xlabel(ax, 'x (m)');   ylabel(ax, 'y (m)');
fprintf('Radio = %.2f m   ->   Area del circulo = %.4f m^2\n', r, A);
end
