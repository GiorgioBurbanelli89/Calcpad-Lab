u = symunit;
% (1) asignacion por elemento en una matriz nueva
K = zeros(2,2);
K(1,1) = 3*u.kN/u.m;
K(1,2) = 2*u.kN;
K(2,1) = 2*u.kN;
K(2,2) = 5*u.kN*u.m;
K
% (2) ensamblaje FEM: Kg(dofs,dofs) = Kg(dofs,dofs) + ke
Kg = zeros(3,3);
ke = [10*u.kN/u.m, -10*u.kN/u.m; -10*u.kN/u.m, 10*u.kN/u.m];
Kg([1 2],[1 2]) = Kg([1 2],[1 2]) + ke;
Kg([2 3],[2 3]) = Kg([2 3],[2 3]) + ke;
Kg
