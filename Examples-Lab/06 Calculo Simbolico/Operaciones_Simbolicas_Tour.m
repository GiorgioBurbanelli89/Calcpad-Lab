%% Operaciones simbólicas en Hekatan Lab — el mismo álgebra que MATLAB
%' <h3>Cálculo simbólico: lo que hace MATLAB, lo hace Hekatan Lab</h3>
%' <hr/>
%' Un recorrido por el álgebra simbólica, TODO renderizado como matemática de libro,
%' sin `disp` ni `fprintf`. Este archivo corre igual en MATLAB (Symbolic Math Toolbox)
%' y en Hekatan Lab: si muestras una variable simbólica, aparece tal cual, tipografiada.

%' <h4>1. Una expresión: expandir y factorizar (operaciones inversas)</h4>
syms x y real
f = (x + 1)^2 * (x - 2)
fe = expand(f)
ff = factor(fe)

%' <h4>2. Cálculo: derivada e integral</h4>
df = simplify(diff(f, x))
F = int(f, x)
%' Integral definida entre 0 y 2:
Fd = int(f, x, 0, 2)

%' <h4>3. Límite y serie de Taylor</h4>
Lim = limit(sin(x)/x, x, 0)
T = taylor(exp(x), x)

%' <h4>4. Resolver una ecuación</h4>
r = solve(x^2 - 5*x + 6 == 0, x)

%' <h4>5. Sustitución / evaluación simbólica</h4>
g = subs(f, x, 3)

%' <h4>6. Identidad trigonométrica</h4>
s = simplify(sin(x)^2 + cos(x)^2)

%' <h4>7. Álgebra simbólica de matrices</h4>
M = [x, y; y, x]
detM = det(M)
invM = simplify(inv(M))
%" Todo esto es Symbolic Math Toolbox estándar — el mismo código corre en MATLAB y en
%" Hekatan Lab, y aquí se renderiza como un libro (sin disp/fprintf).
