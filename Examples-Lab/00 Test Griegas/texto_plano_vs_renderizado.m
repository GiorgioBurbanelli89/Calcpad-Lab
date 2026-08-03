% #md
% -> # Texto plano vs renderizado <-
% -> *Por defecto Hekatan Lab RENDERIZA; con `% #plain` sale literal* <-
% #endmd

% #md
% ## 1) Por defecto: RENDERIZADO
% Los nombres de variables, letras griegas y potencias `^N` se tipografian solos.
% #endmd

fprintf('sigma = %g MPa,  nu = %.2f,  x^2 = %g\n', 250, 0.15, 9)
disp('theta = 30 grados,  area = 25 m^2')

% #md
% ## 2) Con `% #plain`: TEXTO PLANO
% Todo queda literal: alpha, sigma, x^2 sin transformar.
% #endmd

% #plain
fprintf('sigma = %g MPa,  nu = %.2f,  x^2 = %g\n', 250, 0.15, 9)
disp('theta = 30 grados,  area = 25 m^2')
% #render

% #md
% ## 3) `% #render` vuelve a activar el render
% #endmd

fprintf('De nuevo renderizado: alpha beta gamma,  sigma^2\n')

% #md
% ## Importante
% - `#plain` / `#render` solo afectan a `disp` y `fprintf` (texto impreso).
% - Las **formulas `#noc`** y el **echo de variables** SIEMPRE se renderizan.
% #endmd

% #noc sigma_vm = sqrt(sx^2 - sx*sy + sy^2 + 3*txy^2)
E = 2e8      % 'el echo del valor siempre renderiza
