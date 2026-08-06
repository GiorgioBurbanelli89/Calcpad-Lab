%' # Operaciones simbolicas — escalares, funciones, vectores y matrices
%'
%' Corre IGUAL en MATLAB 2017a (Symbolic Math Toolbox) y en Hekatan Lab.
%' Muestra el abanico de operaciones simbolicas renderizadas tipograficamente.

syms x y a b c n real

%' ## 1. Escalares — algebra y calculo
%' Factorizar, expandir, simplificar y colectar:
disp('factor(x^2 - 1) ='),        disp(factor(x^2 - 1))
disp('expand((x + 2)^3) ='),      disp(expand((x + 2)^3))
disp('simplify((x^2-1)/(x-1)) ='),disp(simplify((x^2 - 1)/(x - 1)))
%' Derivada, integral, limite y serie de Taylor:
disp('diff(sin(x)*exp(x)) ='),    disp(diff(sin(x)*exp(x), x))
disp('int(x*exp(x)) ='),          disp(int(x*exp(x), x))
disp('limit(sin(x)/x, 0) ='),     disp(limit(sin(x)/x, x, 0))
disp('taylor(cos(x)) ='),         disp(taylor(cos(x), x, 0))
%' Resolver ecuaciones (raices simbolicas de la cuadratica general):
disp('solve(a*x^2 + b*x + c) ='), disp(solve(a*x^2 + b*x + c, x))

%' ## 2. Funciones y sustitucion
f = x^2 + 3*x + 1;
disp('f(y+1) = subs(f, x, y+1) ='), disp(expand(subs(f, x, y + 1)))
%' La constante pi se mantiene SIMBOLICA (no 3.14159):
disp('area del circulo  A = pi*r^2 ='), disp(sym('pi')*x^2)

%' ## 3. Vectores simbolicos
u = [a; b; 0];
v = [0; a; b];
disp('producto punto  dot(u, v) ='),   disp(dot(u, v))
disp('producto cruz   cross(u, v) ='), disp(cross(u, v).')
disp('derivada del vector  d/dx [x; x^2; x^3] ='), disp(diff([x; x^2; x^3], x).')

%' ## 4. Matrices simbolicas
M = [a, b; b, a];
disp('determinante  det(M) ='),  disp(det(M))
disp('traza  trace(M) ='),        disp(trace(M))
disp('inversa  inv(M) ='),        disp(inv(M))
disp('producto  M*M ='),          disp(M*M)
disp('Kronecker  kron([a b], [1 1]) ='), disp(kron([a b], [1 1]))
%' Sistema lineal simbolico  M x = [1; 0]:
disp('solucion  M \\ [1; 0] ='),  disp((M\[1; 0]).')

%' Todas estas operaciones renderizan como matrices, fracciones y raices
%' tipografiadas — motor simbolico real (giac), la ventaja de Hekatan Lab.
