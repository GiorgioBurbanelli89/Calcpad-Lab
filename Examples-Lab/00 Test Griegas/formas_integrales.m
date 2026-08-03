% #md
% -> # Todas las formas de integrar <-
% -> *Integral simple y doble en Hekatan Lab y MATLAB* <-
% #endmd

% #md
% ## A) Integral SIMPLE
% I = integral de 3x^2 en [0, 2] = **8**  (usamos 5 formas, todas dan 8).
% La formula typeset (con el simbolo integral) va abajo con `#noc`.
% | # | Forma | Comando |
% |---|-------|---------|
% | 1 | Notacion simbolica | `% #noc $Area{...}` |
% | 2 | Operacion simbolica | `int(f,x,a,b)` |
% | 3 | Numerica adaptativa | `integral / quad / quadgk` |
% | 4 | Cuadratura Gauss | loop |
% | 5 | Trapecio (datos) | `trapz` |
% #endmd

%% 1) Notacion simbolica (#noc, Calcpad)
% #noc I = $Area{3*x^2 @ x = 0 : 2}

%% 2) Operacion simbolica: int(f, x, a, b)
syms x
I_sym = int(3*x^2, x, 0, 2)

%% 3) Numerica adaptativa: integral (= quad = quadgk)
I_num = integral(@(x) 3*x.^2, 0, 2)

%% 4) Cuadratura de Gauss-Legendre (loop, 3 puntos)
gp = [-0.774596669241; 0; 0.774596669241];  gw = [5/9; 8/9; 5/9];
a = 0;  b = 2;  I_g = 0;
for k = 1:3
    xk = (a+b)/2 + (b-a)/2*gp(k);
    I_g = I_g + gw(k)*3*xk^2;
end
I_g = (b-a)/2 * I_g

%% 5) Trapecio sobre datos: trapz
xv = linspace(0, 2, 201);
I_tz = trapz(xv, 3*xv.^2)

% #md
% ## B) Integral DOBLE
% I2 = integral doble de x*y en [0,1]x[0,1] = 1/4 = **0.25**  (5 formas, todas dan 0.25).
% La formula typeset (con el doble integral) va abajo con `#noc`.
% #endmd

%% 1) Notacion simbolica (#noc, Calcpad)
% #noc I_2 = $Area{$Area{x*y @ x = 0 : 1} @ y = 0 : 1}

%% 2) Operacion simbolica: int(int(...))
syms y
I2_sym = int(int(x*y, x, 0, 1), y, 0, 1)

%% 3) Numerica adaptativa: integral2 (= dblquad)
I2_num = integral2(@(x,y) x.*y, 0, 1, 0, 1)

%% 4) Cuadratura de Gauss doble (loop, 2x2)
g2 = [-0.577350269190; 0.577350269190];  w2 = [1; 1];
I2_g = 0;
for i = 1:2
    for j = 1:2
        xi = (g2(i)+1)/2;  yj = (g2(j)+1)/2;
        I2_g = I2_g + w2(i)*w2(j)*xi*yj;
    end
end
I2_g = I2_g/4

%% 5) Trapecio doble: trapz en x (por fila) y luego en y
xg = linspace(0,1,101);  yg = linspace(0,1,101);
[X, Y] = meshgrid(xg, yg);
Z = X.*Y;
Ix = zeros(numel(yg), 1);
for r = 1:numel(yg)
    Ix(r) = trapz(xg, Z(r, :));
end
I2_tz = trapz(yg, Ix)
