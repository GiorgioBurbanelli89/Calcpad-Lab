% 02_Operaciones_Simbolicas.m
% =========================================================================
% EJEMPLO 2 — Operaciones simbolicas + texto en una linea
% =========================================================================
% Demuestra los patrones para combinar simbolico y texto en MATLAB / Hekatan Lab.
% Clave: char(expr) convierte la expresion simbolica a string para fprintf.
%
% En Hekatan Lab MATLAB-mode, char() devuelve HTML con CSS Hekatan: las
% expresiones se renderizan con fracciones apiladas, variables azules,
% exponentes como superindice, etc.
% =========================================================================

clear; clc;
syms x y a b c

fprintf('=== 1) Variables simbolicas ===\n');
fprintf('Declaracion: syms x y a b c\n\n');

% ---- Derivada ----
f = x^3 + 2*x^2 - 5*x + 7;
df = diff(f, x);
fprintf('=== 2) Derivada ===\n');
fprintf('f(x) = %s\n', char(f));
fprintf('df/dx = %s\n\n', char(df));

% ---- Integral ----
% NOTA: el motor simbolico MVP soporta int de polinomios y trigonometricas
% simples. Productos generales (sin(x)*x) requieren integracion por partes
% que no esta en el MVP — usar caso polinomico aqui.
g = x^3 + 2*x;
intg = int(g, x);
fprintf('=== 3) Integral ===\n');
fprintf('int %s dx = %s\n\n', char(g), char(intg));

% ---- Limite ----
fprintf('=== 4) Limite ===\n');
fprintf('lim x->0 de sin(x)/x = %s\n\n', char(limit(sin(x)/x, x, 0)));

% ---- Resolver ecuacion (sin == — pasar la expresion asumiendo =0) ----
% NOTA: Hekatan Lab MVP no soporta `solve(expr == 0, x)`.
%       Usar `solve(expr, x)` (asume = 0).
fprintf('=== 5) Resolver ecuacion (sin ==) ===\n');
sol = solve(x^2 - 4, x);
% sol es un array — mostrar elemento por elemento
for k = 1:length(sol)
    fprintf('Raiz %d: x = %s\n', k, char(sol(k)));
end
fprintf('\n');

% ---- Simplificacion ----
fprintf('=== 6) Simplificacion ===\n');
expr = (sin(x)^2 + cos(x)^2) * (x^2 - 1)/(x-1);
fprintf('Original:   %s\n', char(expr));
fprintf('Simplify:   %s\n\n', char(simplify(expr)));

% ---- Sustitucion ----
fprintf('=== 7) Sustitucion numerica ===\n');
f1 = diff(x^3 - 4*x);
fprintf('f(x) = %s\n', char(f1));
fprintf('f(2) = %g\n\n', double(subs(f1, x, 2)));

% ---- Matriz simbolica ----
fprintf('=== 8) Matriz simbolica ===\n');
M = [a b; c x];
fprintf('det([a b; c x]) = %s\n\n', char(det(M)));

% ---- LaTeX ----
% NOTA: int(1/(x^2+1)) = atan(x) requiere regla especial (no en MVP).
% Usamos un polinomio que el motor sí integra.
fprintf('=== 9) LaTeX para reportes ===\n');
poly = x^2 + 3*x;
fprintf('LaTeX de int (%s) dx:\n', char(poly));
fprintf('  $$%s$$\n\n', latex(int(poly)));

% ---- Texto + simbolico + numerico en un solo fprintf ----
fprintf('=== 10) Combinado en una linea ===\n');
h = x^2 + 3*x;
fprintf('Para h = %s tenemos h''(x) = %s, int h dx = %s, h(2) = %g\n', ...
    char(h), char(diff(h)), char(int(h)), double(subs(h, x, 2)));

fprintf('\n=== FIN OPERACIONES SIMBOLICAS ===\n');
