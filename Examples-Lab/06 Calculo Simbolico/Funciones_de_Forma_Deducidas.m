%% Funciones de forma de Hermite — DEDUCIDAS por el programa (no tecleadas)
%' <h3>Funciones de forma de una viga: CALCULADAS, no escritas a mano</h3>
%' <hr/>
%' Pregunta típica: «¿los polinomios los tecleaste o los calcula el programa?».
%' Aquí NADIE teclea los polinomios: se DEDUCEN con álgebra simbólica. Este mismo
%' archivo corre igual en MATLAB (Symbolic Toolbox) y en Hekatan Lab.

%' <h4>1. Viga cúbica genérica (4 constantes por hallar)</h4>
syms x L c_0 c_1 c_2 c_3 real
w = c_0 + c_1*x + c_2*x^2 + c_3*x^3
%' Su giro es la derivada:
dw = diff(w, x)

%' <h4>2. Las 4 condiciones nodales — las evalúa el motor</h4>
%' Grados de libertad de la viga (flechas y giros en los dos nudos). El motor los
%' obtiene sustituyendo los extremos x=0 y x=L (nadie escribe los resultados):
u = [subs(w, x, 0); subs(dw, x, 0); subs(w, x, L); subs(dw, x, L)]

%' <h4>3. Matriz del sistema — deducida con jacobian</h4>
%' Las condiciones son lineales en las constantes. La matriz que las relaciona se
%' obtiene derivando respecto de las constantes (tampoco se teclea):
C = jacobian(u, [c_0; c_1; c_2; c_3])

%' <h4>4. Se invierte y salen las funciones de forma</h4>
%' La base de monomios del polinomio:
mon = [1, x, x^2, x^3]
%' Invirtiendo el sistema, las funciones de forma son la fila  mon·C⁻¹:
N = simplify(mon * inv(C))

%' <h4>5. Cada polinomio de Hermite por separado</h4>
N1 = simplify(N(1))
N2 = simplify(N(2))
N3 = simplify(N(3))
N4 = simplify(N(4))
%" Exactamente los polinomios de Hermite de cualquier libro de FEM — pero aquí los
%" produjo el motor simbólico, no el teclado.

%' <h4>6. Tiempo de la deducción — tic/toc (comparable con MATLAB)</h4>
%' La MISMA deducción, cronometrada. Corre este archivo también en MATLAB y compara
%' el número: es álgebra simbólica idéntica en ambos.
tic
u_t = [subs(w, x, 0); subs(dw, x, 0); subs(w, x, L); subs(dw, x, L)];
C_t = jacobian(u_t, [c_0; c_1; c_2; c_3]);
N_t = simplify([1, x, x^2, x^3] * inv(C_t));
t_deduccion_seg = toc
