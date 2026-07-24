function d=cmp3d(A3,Afl,ne)
% Lee el 3-D DENTRO de una funcion (como fint_of lee sig0) y lo compara
% contra el equivalente aplanado.
d=0;
for e=1:ne
  for q=1:3
    a=A3(:,e,q); b=Afl(:,(e-1)*3+q);
    v=max(abs(a-b));
    if v>d; d=v; end
  end
end
