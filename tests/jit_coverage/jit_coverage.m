function jit_coverage()
% Barrido de cobertura del JIT: funciones de todo tipo (escalar, vector, matriz,
% for, while, break/continue, multi-salida, vacio/isempty, indexado, inversa, solve).
% Cada prueba devuelve UN escalar. Se corre en MATLAB (referencia) y en Hekatan Lab;
% los valores deben coincidir. El nombre + valor se imprime una linea por prueba.
r = zeros(1,22);
r(1)  = t_scalar(1.3);
r(2)  = t_poly(2.0);
r(3)  = t_forloop(1000);
r(4)  = t_while(10.0);
r(5)  = t_breakcont(100);
r(6)  = t_recursion(12);
r(7)  = t_vecbuild(200);
r(8)  = t_dot(50);
r(9)  = t_matmul();
r(10) = t_transpose();
r(11) = t_ewise();
r(12) = t_matadd();
r(13) = t_inv();
r(14) = t_solve();
r(15) = t_norm();
r(16) = t_reduce(30);
r(17) = t_indexcol();
r(18) = t_multiout(7.0);
r(19) = t_empty(1);
r(20) = t_empty(0);
r(21) = t_newton();
r(22) = t_nestedloops(40);
nm = {'scalar_math','poly','for_loop','while_loop','break_continue','recursion', ...
      'vec_build_sum','vec_dot','matmul','transpose','elementwise','mat_add_sub', ...
      'inverse','linsolve','norm','reduce_max','index_col','multi_output', ...
      'empty_true','empty_false','newton','nested_loops'};
for i=1:numel(r)
  fprintf('%2d  %-16s = %.10f\n', i, nm{i}, r(i));
end
end

% ---------- escalares ----------
function y=t_scalar(x)
y = x^2 + 3*x - sin(x)/cos(x+1) + sqrt(abs(x)) + exp(-x);
end
function y=t_poly(x)
y=0; c=[1 -2 3 -4 5];
for i=1:5, y = y*x + c(i); end          % Horner
end
function y=t_recursion(n)                 % fibonacci recursivo (escalar, se llama a si misma)
if n<2, y=n; else y=t_recursion(n-1)+t_recursion(n-2); end
end
function y=t_newton()                      % Newton: raiz de x^2-2
x=1.0;
for k=1:30, x = x - (x*x-2)/(2*x); end
y=x;
end

% ---------- for / while / break-continue ----------
function s=t_forloop(n)
s=0; for i=1:n, s = s + i*i - mod(i,3); end
end
function it=t_while(x)
it=0; s=0;
while s<x, it=it+1; s = s + 1/it; end     % armonica hasta superar x
end
function s=t_breakcont(n)
s=0;
for i=1:n
  if mod(i,7)==0, continue; end
  if i>50, break; end
  s = s + i;
end
end
function s=t_nestedloops(n)
s=0;
for i=1:n
  for j=1:i
    s = s + mod(i*j,5);
  end
end
end

% ---------- vectores ----------
function s=t_vecbuild(n)
v=zeros(1,n);
for i=1:n, v(i) = sin(i*0.1) + cos(i*0.2); end
s = sum(v);
end
function d=t_dot(n)
a = 1:n; b = (1:n)*2 - 1;
d = a*b';                                  % fila * columna = escalar
end

% ---------- matrices ----------
function y=t_matmul()
A=[1 2; 3 4]; B=[5 6; 7 8]; C=A*B;
y = C(1,1) + C(2,2);
end
function y=t_transpose()
A=[1 2 3; 4 5 6]; B=A';
y = B(3,2) + B(1,2);
end
function y=t_ewise()
A=[1 2; 3 4]; B = A.*A + A./2 - A;
y = B(1,1)+B(1,2)+B(2,1)+B(2,2);
end
function y=t_matadd()
A=[1 2; 3 4]; B = A + A - A.*2 + A';
y = B(1,2) + B(2,1);
end
function y=t_inv()
A=[4 1; 2 3]; B=inv(A);
y = B(1,1) + B(2,2);
end
function y=t_solve()
A=[3 1; 1 2]; b=[9; 8]; x = A\b;
y = x(1) + x(2);
end
function y=t_norm()
v=[3; 4; 12]; y = norm(v);
end
function y=t_reduce(n)
v=zeros(1,n); for i=1:n, v(i)=mod(i*i,13); end
y = max(v) + min(v) + sum(v);
end
function y=t_indexcol()
A=[1 2 3; 4 5 6; 7 8 9]; c = A(:,2); r = A(2,:);
y = sum(c) + sum(r);
end

% ---------- multi-salida / vacio ----------
function y=t_multiout(x)
[a,b] = minmax([x 1 4 1 5 9 2 6]);
y = a*100 + b;
end
function [lo,hi]=minmax(v)
lo=v(1); hi=v(1);
for i=2:numel(v)
  if v(i)<lo, lo=v(i); end
  if v(i)>hi, hi=v(i); end
end
end
function y=t_empty(flag)
r = maybe(flag);
if isempty(r), y = -1; else y = r(1)+r(2); end
end
function r=maybe(flag)
if flag>0, r=[10 20 30]; else r=[]; end
end
