% #md
% # Polinomios simbolicos en Hekatan Lab
% Los mismos polinomios, ahora en **algebra simbolica** (como el Symbolic Toolbox de
% MATLAB). Cada resultado se **renderiza como matematica de libro**, no como texto.
% #endmd

syms s

% #md
% ## 1. El polinomio  p(s)
% #endmd
p = expand(s^2 - 5*s + 6)

% #md
% ## 2. Derivada  (equivale a polyder)
% #endmd
dp = diff(p, s)

% #md
% ## 3. Integral  (equivale a polyint)
% #endmd
Ip = expand(int(p, s))

% #md
% ## 4. Forma factorizada
% #endmd
pf = factor(p)

% #md
% ## 5. Raices  (solve)  ->  deben ser 2 y 3
% #endmd
r = solve(p == 0, s)

% #md
% ## 6. Evaluacion simbolica  polyval([1 -5 6], s)
% #endmd
q = polyval([1 -5 6], s)

% #md
% ## 7. Evaluacion en  s = 4   ->  16 - 20 + 6 = 2
% #endmd
p4 = subs(p, s, 4)

% #md
% ## 8. Producto de binomios  (equivale a conv)   (s+2)(s+3)
% #endmd
c = expand((s + 2)*(s + 3))
