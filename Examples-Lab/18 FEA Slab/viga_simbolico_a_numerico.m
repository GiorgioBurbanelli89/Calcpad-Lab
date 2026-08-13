%% Viga Euler-Bernoulli: del cálculo SIMBÓLICO al resultado con NÚMEROS
%' <h3>Rigidez de viga: deducción simbólica → resultado numérico</h3>
%' <hr/>
%' Deducimos TODO con álgebra simbólica —incluidas las funciones de forma— y luego
%' lo evaluamos con valores reales para calcular una deflexión. El MISMO motor hace
%' el símbolo y el número, sin salir del reporte y sin usar disp/fprintf.

%' <h4>1. Funciones de forma de Hermite — CALCULADAS, no escritas a mano</h4>
%' Aquí NO se teclean los polinomios: se DEDUCEN. Se parte de una viga cúbica
%' genérica con 4 constantes por hallar:
syms x L c_0 c_1 c_2 c_3 real
w = c_0 + c_1*x + c_2*x^2 + c_3*x^3
dw = diff(w, x);
%' Sus 4 condiciones nodales (flechas y giros en los dos nudos) las evalúa el motor
%' sustituyendo los extremos x=0 y x=L:
u = [subs(w, x, 0); subs(dw, x, 0); subs(w, x, L); subs(dw, x, L)]
%' La matriz del sistema se deduce derivando respecto de las constantes (jacobian):
C = jacobian(u, [c_0; c_1; c_2; c_3])
%' Se invierte y las funciones de forma son la fila  mon·C⁻¹  (mon = base de monomios):
mon = [1, x, x^2, x^3];
N = simplify(mon * inv(C))
%' El motor devuelve EXACTAMENTE los polinomios de Hermite — nadie los tecleó.

%' <h4>2. Curvaturas (segundas derivadas)</h4>
%' N'' se escribe con el token `Npprime`:
Npprime = simplify(diff(N, x, 2))

%' <h4>3. Matriz de rigidez SIMBÓLICA</h4>
%' Integrando el producto de curvaturas por la rigidez a flexión EI (constante),
%' la propia integral se renderiza al mostrar el resultado:
syms EI real
K_sym = simplify(EI * int(Npprime.' * Npprime, x, 0, L))

%' <h4>4. Ahora con VALORES</h4>
% Datos (ocultos): E en kN/m², I en m⁴, L en m
E = 210e6;  Iz = 8.333e-6;  Lval = 4;
%' Módulo  E = @E kN/m² ,  inercia  I = @Iz m⁴ ,  longitud  L = @Lval m
%' Sustituimos EI y L en la matriz simbólica → matriz de rigidez NUMÉRICA:
K = double(subs(K_sym, {EI, L}, {E*Iz, Lval}))

%' <h4>5. Deflexión de un voladizo con carga P en la punta</h4>
P = 10;   % kN (oculto)
%' Empotrado en el nudo 1 (gdl w_1, θ_1 fijos); carga  P = @P kN hacia abajo en el nudo 2.
Kff = K(3:4, 3:4);            % gdl libres: w_2, θ_2
F = [-P; 0];                 % fuerza en w_2 (sin momento)
d = Kff \ F;                 % desplazamientos libres
w2_mm = d(1)*1000            %' Deflexión en la punta  w_2 = @ mm
%' Fórmula clásica del voladizo  P·L³/(3·E·I), para comparar:
w_teo_mm = -P*Lval^3/(3*E*Iz)*1000   %' teórica  = @ mm
%" El símbolo se convierte en número — y coincide con la teoría.
