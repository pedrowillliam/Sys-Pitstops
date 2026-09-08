import { Navigate, createBrowserRouter, RouterProvider } from 'react-router'
import { LoginPage } from './auth/LoginPage'
import { RequireAuth, RequireRole } from './auth/RequireAuth'
import { useSession } from './auth/session'
import { CustomerFormPage } from './customers/CustomerFormPage'
import { CustomersPage } from './customers/CustomersPage'
import { KanbanPage } from './service-orders/KanbanPage'
import { AppShell } from './layout/AppShell'
import { landingFor } from './layout/navigation'
import { NotFound, Placeholder } from './pages/Placeholder'

function Landing() {
  const { data: user } = useSession()
  return <Navigate to={landingFor(user?.role ?? '')} replace />
}

const desk = ['ADMIN', 'ATTENDANT']

const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppShell />
      </RequireAuth>
    ),
    children: [
      { index: true, element: <Landing /> },
      {
        path: 'dashboard',
        element: (
          <RequireRole allowed={['ADMIN']}>
            <Placeholder title="Dashboard" waitingFor="os KPIs do §6 do data-model" />
          </RequireRole>
        ),
      },
      {
        path: 'service-orders',
        element: <KanbanPage />,
      },
      {
        path: 'my-queue',
        element: (
          <RequireRole allowed={['MECHANIC']}>
            <Placeholder title="Minha fila" waitingFor="a API de ordens de serviço" />
          </RequireRole>
        ),
      },
      {
        path: 'mechanics',
        element: (
          <RequireRole allowed={desk}>
            <Placeholder title="Mecânicos" waitingFor="a API de usuários" />
          </RequireRole>
        ),
      },
      {
        path: 'customers',
        element: (
          <RequireRole allowed={desk}>
            <CustomersPage />
          </RequireRole>
        ),
      },
      // `new` before `:id` so the literal wins the match.
      {
        path: 'customers/new',
        element: (
          <RequireRole allowed={desk}>
            <CustomerFormPage />
          </RequireRole>
        ),
      },
      {
        path: 'customers/:id',
        element: (
          <RequireRole allowed={desk}>
            <CustomerFormPage />
          </RequireRole>
        ),
      },
      // Sem item no menu de propósito: o protótipo não tem seção de veículos,
      // eles aparecem dentro do cliente e da OS. A rota existe porque a API
      // existe e a tela de cliente vai apontar para cá.
      {
        path: 'vehicles',
        element: (
          <RequireRole allowed={desk}>
            <Placeholder title="Veículos" waitingFor="o merge do PR de clientes e veículos" />
          </RequireRole>
        ),
      },
      {
        path: 'quotes',
        element: (
          <RequireRole allowed={desk}>
            <Placeholder title="Orçamento" waitingFor="a API de orçamentos" />
          </RequireRole>
        ),
      },
      {
        path: 'inventory',
        element: (
          <RequireRole allowed={desk}>
            <Placeholder title="Estoque" waitingFor="a API de peças e movimentações" />
          </RequireRole>
        ),
      },
      {
        path: 'settings',
        element: (
          <RequireRole allowed={['ADMIN']}>
            <Placeholder title="Configurações" waitingFor="a definição do que é configurável" />
          </RequireRole>
        ),
      },
      { path: '*', element: <NotFound /> },
    ],
  },
])

export function App() {
  return <RouterProvider router={router} />
}
