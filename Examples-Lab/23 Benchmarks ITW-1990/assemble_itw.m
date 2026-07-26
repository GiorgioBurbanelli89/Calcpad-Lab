function K = assemble_itw(x_j, z_j, e_j, D, gam, t)
% Ensambla la rigidez global de una malla de membranas ITW (3 GDL/nudo: u,v,rz).
n = 3;  n_j = numel(x_j);  n_g = n*n_j;  K = zeros(n_g, n_g);
for e = 1:size(e_j,1)
    q  = e_j(e,:)';
    Ke = ke_itw(x_j(q), z_j(q), D, gam, t);
    for i = 1:4
        for j = 1:4
            i1 = n*(e_j(e,i)-1);  i2 = n*(e_j(e,j)-1);
            K(i1+1:i1+3, i2+1:i2+3) = K(i1+1:i1+3, i2+1:i2+3) + ...
                Ke(n*(i-1)+1:n*i, n*(j-1)+1:n*j);
        end
    end
end
end
