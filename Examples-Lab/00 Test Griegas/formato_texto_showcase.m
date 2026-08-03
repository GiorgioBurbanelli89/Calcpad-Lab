% #md
% -> # Formato de texto en Hekatan Lab <-
% -> *Todas las posibilidades: titulos, tablas, texto y renderizado* <-
% #endmd

% #md
% ## 1. Titulos y encabezados
% - `%% Seccion`  ->  encabezado de seccion (MATLAB tambien lo usa como celda)
% - `#`, `##`, `###` dentro de `% #md`  ->  H1 / H2 / H3
% - Centrado: envolver la linea con  `-> texto <-`
% #endmd

%% 2. Texto: negrita, cursiva, codigo, listas
% #md
% Texto normal con **negrita**, *cursiva* y `codigo`. Y una lista:
% - primer punto
% - segundo punto
% #endmd

%% 3. Tabla resumen (Markdown, columnas alineadas)
% #md
% | Metodo                | Tiempo (s) | Exacto |
% |-----------------------|------------|--------|
% | Simbolica `int(int)`  | 1.10       | si     |
% | Loop Gauss 4x4        | 0.02       | no     |
% #endmd

%% 4. disp / fprintf / sprintf  (formas de MATLAB)
disp('Texto con disp')
fprintf('fprintf con formato: area = %d m^2\n', 25)
s = sprintf('sprintf guarda en variable: pi = %.4f', pi)

%% 5. Combinado TEXTO + RENDERIZADO (por defecto)
% En fprintf/disp los nombres, letras griegas y ^N se renderizan solos:
fprintf('Modulo E = %g kN/m^2,  sigma = %g MPa,  nu = %.2f\n', 35000, 250, 0.15)
% Variable con descripcion visible (apostrofo = texto Calcpad):
E = 35000    % 'Modulo elastico E [kN/m^2]

%% 6. Formula tipografiada (#noc): solo notacion, sin calcular
% #noc sigma = M*y/I
% #noc D = E*t^3/(12*(1 - nu^2))

%% 7. Texto PLANO vs RENDERIZADO (se activa dentro de %)
% #plain
fprintf('PLANO:     sigma = x^2 + nu\n')
% #render
fprintf('RENDER:    sigma = x^2 + nu\n')

%% 8. Comentario oculto y variables (echo automatico, sin disp)
%-- este comentario NO aparece en el output
t = 0.1
v = [1 2 3 4]
