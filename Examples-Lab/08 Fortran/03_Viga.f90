program viga_simplemente_apoyada
    ! Hekatan Fortran - viga simplemente apoyada con carga uniforme.
    ! Notacion de ingenieria con subindices: M_max, V_max, d_max, I_y...
    implicit none

    integer :: i, S_n
    real    :: w, L, E, I_y
    real    :: M_max, V_max, d_max, d_adm

    ! ---- 1) El loop de siempre: sumar 1..10 ----
    S_n = 0
    do i = 1, 10
        S_n = S_n + i
    end do
    print *, 'S_n  (suma 1..10) =', S_n

    ! ---- Clasico de Fortran: division entera vs real ----
    print *, '7/2    (enteros)  =', 7/2
    print *, '7.0/2.0 (reales)  =', 7.0/2.0
    print *, ''

    ! ---- 2) Datos de la viga ----
    w   = 25.0        ! kN/m   carga distribuida
    L   = 6.0         ! m      luz
    E   = 200.0e6     ! kPa    modulo de elasticidad
    I_y = 8.5e-5      ! m^4    inercia respecto a y

    print *, 'DATOS'
    print *, '  w   =', w, 'kN/m'
    print *, '  L   =', L, 'm'
    print *, '  E   =', E, 'kPa'
    print *, '  I_y =', I_y, 'm^4'
    print *, ''

    ! ---- 3) Solicitaciones ----
    M_max = w * L**2 / 8.0                        ! momento maximo
    V_max = w * L / 2.0                           ! cortante en apoyo
    d_max = 5.0 * w * L**4 / (384.0 * E * I_y)    ! flecha maxima
    d_adm = L / 250.0                             ! flecha admisible

    print *, 'RESULTADOS'
    print *, '  M_max =', M_max, 'kN*m'
    print *, '  V_max =', V_max, 'kN'
    print *, '  d_max =', d_max * 1000.0, 'mm'
    print *, '  d_adm =', d_adm * 1000.0, 'mm   (L/250)'
    print *, ''

    ! ---- 4) Verificacion de servicio ----
    if (d_max <= d_adm) then
        print *, 'CUMPLE:  d_max <= d_adm'
    else
        print *, 'NO CUMPLE:  d_max > d_adm  -> aumentar I_y'
    end if

end program viga_simplemente_apoyada
