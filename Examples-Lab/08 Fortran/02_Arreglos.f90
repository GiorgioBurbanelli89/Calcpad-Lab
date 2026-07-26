program arreglos
    ! Arreglos en Fortran: cargas de piso de un portico.
    ! Notacion con subindices: P_i, h_i, P_tot, V_base, h_tot...
    implicit none

    integer, parameter :: n_pisos = 5
    real    :: P_i(5), h_i(5)
    real    :: P_tot, V_base, h_tot, P_max
    integer :: i

    ! ---- Cargar los arreglos (ojo: en Fortran los indices empiezan en 1) ----
    do i = 1, n_pisos
        P_i(i) = 25.0 + 5.0 * i      ! kN  carga del piso i
        h_i(i) = 3.0 * i             ! m   altura acumulada del piso i
    end do

    print *, 'PISO      P_i(kN)     h_i(m)'
    do i = 1, n_pisos
        print *, i, P_i(i), h_i(i)
    end do
    print *, ''

    ! ---- Intrinsecas sobre arreglos ----
    P_tot = sum(P_i)
    P_max = maxval(P_i)
    h_tot = maxval(h_i)

    print *, 'n_pisos =', size(P_i)
    print *, 'P_tot   =', P_tot, 'kN'
    print *, 'P_max   =', P_max, 'kN'
    print *, 'h_tot   =', h_tot, 'm'

    ! ---- Cortante basal acumulado (la misma suma, pero con loop) ----
    V_base = 0.0
    do i = 1, n_pisos
        V_base = V_base + P_i(i)
    end do
    print *, 'V_base  =', V_base, 'kN'
    print *, ''

    ! ---- Verificacion: el loop y sum() deben coincidir ----
    if (abs(V_base - P_tot) < 0.001) then
        print *, 'OK:  V_base = P_tot  (el loop y sum() coinciden)'
    else
        print *, 'ERROR:  V_base /= P_tot'
    end if

end program arreglos
