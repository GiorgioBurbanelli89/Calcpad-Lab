%% Texto + variables como un LIBRO — todas las formas (Hekatan Lab)
%" Combinar texto y variables como un libro

%%
%" 1. Prosa con variables (condicionadas ARRIBA, antes de definirlas)
%' Analizamos una losa rectangular simplemente apoyada de @a m por @b m, de espesor
%' @t m, bajo una carga uniforme de @q kN/m². El material tiene modulo de elasticidad
%' @E MPa y coeficiente de Poisson @nu. El area de la losa es @{a*b} m² y su rigidez
%' a flexion por unidad de ancho es @{E*t^3/(12*(1-nu^2))*1000} kN·m.
% --- las variables se definen aqui abajo, con comentario OCULTO (nota privada) ---
a = 6;      % largo (oculto)
b = 4;      % ancho (oculto)
t = 0.1;    % espesor (oculto)
q = 10;     % carga (oculto)
E = 35000;  % modulo (oculto)
nu = 0.15;  % Poisson (oculto)

%%
%" 2. Definir y describir en la MISMA linea (@ pegado)
% La variable va primero (regla MATLAB); el @ marca donde cae la ecuacion.
n_a = 6            %' Numero de elementos en a: @
n_e = n_a*n_a      %' Total de elementos: @  (texto antes)
a_1 = a/n_a        %' @ es el ancho de cada elemento, en m   (texto despues)
k_flex = E*t^3/(12*(1-nu^2))*1000  %' Rigidez a flexion D = @ kN·m   (mezclado)

%%
%" 3. Formato de texto (negrita, italica, centrado, linea)
%'* Esto va en negrita
%'/ Esto va en italica
%'| Esto va centrado
%'-----
%' Y esto es texto normal con una variable embebida: la carga es @q kN/m².

%%
%" 4. Bloque en columnas (#cols) con las variables
% #cols Largo a | Ancho b | Carga q
a ; b ; q
% #endcols

%%
%" 5. Alineacion: izquierda / centrado / derecha (con variables)
%'< Izquierda — largo a = @a m
%'| Centrado — ancho b = @b m
%'> Derecha — carga q = @q kN/m²
%'>* Derecha + negrita — area = @{a*b} m²

%%
%" 6. Operaciones de texto (MATLAB)
titulo = sprintf('Losa %gx%g m', a, b)   %' El identificador es: @
%' Resumen: losa de @a x @b m, area @{a*b} m², carga @q kN/m².
