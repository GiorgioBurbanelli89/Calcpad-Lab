function jit_coverage2()
% Batch 2: operaciones mas dificiles para hallar HUECOS del JIT.
% Potencias, indexado logico, concatenacion, reshape, find, sort, sum por dim,
% A(:), diag/det/trace, cumsum, linspace, repmat, matriz^n, mean/prod, abs/sign.
% Cada prueba devuelve UN escalar; debe coincidir con MATLAB.
r=zeros(1,20);
r(1)=t_powers();
r(2)=t_compare();
r(3)=t_logidx();
r(4)=t_mathvec();
r(5)=t_concat();
r(6)=t_flatten();
r(7)=t_sumdim();
r(8)=t_reshape();
r(9)=t_repmat();
r(10)=t_find();
r(11)=t_sort();
r(12)=t_diagtrace();
r(13)=t_det();
r(14)=t_cumsum();
r(15)=t_linspace();
r(16)=t_matpow();
r(17)=t_meanprod();
r(18)=t_absign();
r(19)=t_minmaxvec();
r(20)=t_gauss();
nm={'powers','compare','logical_index','math_on_vec','concat','flatten_colon', ...
    'sum_by_dim','reshape','repmat','find','sort','diag_trace','det','cumsum', ...
    'linspace','matrix_power','mean_prod','abs_sign','minmax_vec','gauss_elim'};
for i=1:numel(r)
  fprintf('%2d  %-16s = %.10f\n', i, nm{i}, r(i));
end
end

function y=t_powers()
A=[1 2; 3 4]; B=A.^2 + 2.^A; y=B(1,1)+B(2,2) + 2^10;
end
function y=t_compare()
v=1:10; c=(v>5); y=sum(c);
end
function y=t_logidx()
v=[3 -1 4 -1 5 -9 2 -6]; p=v(v>0); y=sum(p);
end
function y=t_mathvec()
v=[1 4 9 16 25]; y=sum(sqrt(v)) + sum(log(v)) - sum(exp(-v));
end
function y=t_concat()
a=[1 2 3]; b=[4 5 6]; c=[a b]; d=[a; b]; y=sum(c) + d(2,1) + d(1,3);
end
function y=t_flatten()
A=[1 2; 3 4]; v=A(:); y=sum(v)+numel(v);
end
function y=t_sumdim()
A=[1 2 3; 4 5 6]; rr=sum(A,1); cc=sum(A,2); y=sum(rr)+cc(1)+cc(2);
end
function y=t_reshape()
v=1:12; A=reshape(v,3,4); y=A(2,3)+A(3,4);
end
function y=t_repmat()
A=[1 2]; B=repmat(A,2,3); y=sum(B(:));
end
function y=t_find()
v=[0 3 0 5 0 7 0]; idx=find(v); y=sum(idx)+numel(idx);
end
function y=t_sort()
v=[3 1 4 1 5 9 2 6]; s=sort(v); y=s(1)*100+s(end);
end
function y=t_diagtrace()
A=[1 2 3; 4 5 6; 7 8 9]; d=diag(A); y=sum(d)+trace(A);
end
function y=t_det()
A=[4 3; 6 3]; y=det(A);
end
function y=t_cumsum()
v=1:5; c=cumsum(v); y=c(end)+c(3);
end
function y=t_linspace()
v=linspace(0,1,5); y=sum(v)+v(3);
end
function y=t_matpow()
A=[2 0; 1 3]; B=A^2; y=B(1,1)+B(2,1)+B(2,2);
end
function y=t_meanprod()
v=[2 4 6 8]; y=mean(v)+prod([1 2 3 4]);
end
function y=t_absign()
v=[-3 2 -5 7]; y=sum(abs(v))+sum(sign(v));
end
function y=t_minmaxvec()
v=[3 -1 4 -1 5 -9]; y=max(v)*10+min(v);
end
function y=t_gauss()
A=[2 1 -1; -3 -1 2; -2 1 2]; b=[8; -11; -3]; x=A\b; y=x(1)+x(2)+x(3);
end
