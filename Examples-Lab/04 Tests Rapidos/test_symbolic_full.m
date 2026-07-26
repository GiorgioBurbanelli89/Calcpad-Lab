%% Test simbolico completo - paridad con MATLAB R2017a
% Todas las operaciones del Symbolic Math Toolbox que Hekatan Lab soporta.

syms x y

% --- (1) Expresiones simbolicas ---
f = x^2 + 2*x + 1
g = (x + y)^3

% --- (2) expand ---
expand_g = expand(g)

% --- (3) Derivadas ---
df_dx   = diff(f, x)
d2f_dx2 = diff(f, x, 2)

% --- (4) Derivadas parciales ---
dg_dx = diff(g, x)
dg_dy = diff(g, y)

% --- (5) Integrales 1D ---
F_indef = int(f, x)
F_def   = int(f, x, 0, 3)

% --- (6) Integral doble (anidada) ---
int_doble = int(int(x*y, x, 0, 1), y, 0, 1)

% --- (7) Integral simbolica con variable libre ---
int_libre = int(x*y, x, 0, 1)

% --- (8) solve con == (sintaxis MATLAB nativa) ---
raices_f = solve(f == 0, x)

% --- (9) factor ---
fac1 = factor(x^2 - 4)
fac2 = factor(x^2 - 5*x + 6)
fac3 = factor(x^3 - x)

% --- (10) taylor con 'Order' name-value ---
taylor_exp = taylor(exp(x), 'Order', 5)
taylor_sin = taylor(sin(x), 'Order', 7)
taylor_cos = taylor(cos(x), 'Order', 6)

% --- (11) limit ---
lim_sinc = limit(sin(x)/x, x, 0)

% --- (12) subs ---
f_en_5 = subs(f, x, 5)
