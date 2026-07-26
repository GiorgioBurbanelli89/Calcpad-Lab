% MISMO cubo tet que test_solid_val.py, con solidmesh (visor THREE.js jet_r, = Suite Py).
n=3; L=1;
X=zeros((n+1)^3,3); p=0;
for k=0:n, for j=0:n, for i=0:n
  p=p+1; X(p,:)=[i*L/n j*L/n k*L/n];
end, end, end
id=@(i,j,k) i + j*(n+1) + k*(n+1)*(n+1) + 1;
loc=[0 1 2 6; 0 2 3 6; 0 3 7 6; 0 7 4 6; 0 4 5 6; 0 5 1 6];
corn=[0 0 0;1 0 0;1 1 0;0 1 0;0 0 1;1 0 1;1 1 1;0 1 1];
T=zeros(6*n*n*n,4); e=0;
for k=0:n-1, for j=0:n-1, for i=0:n-1
  gi=zeros(1,8);
  for c=1:8, gi(c)=id(i+corn(c,1), j+corn(c,2), k+corn(c,3)); end
  for t=1:6, e=e+1; T(e,:)=gi(loc(t,:)+1); end
end, end, end
C=X(:,3);                       % campo por NODO = z
solidmesh(T, X, C);
