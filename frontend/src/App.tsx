import { Navigate, createBrowserRouter, RouterProvider } from 'react-router'
import { LoginPage } from './auth/LoginPage'
import { PublicQuotePage } from './quotes/PublicQuotePage'
import { QuotesPage } from './quotes/QuotesPage'
import { RequireAuth, RequireRole } from './auth/RequireAuth'
import { useSession } from './auth/session'
import { CustomerFormPage } from './customers/CustomerFormPage'
import { CustomersPage } from './customers/CustomersPage'
import { InventoryPage } from './inventory/InventoryPage'
import { PartFormPage } from './inventory/PartFormPage'
import { KanbanPage } from './service-orders/KanbanPage'
import { OpenOrderPage } from './service-orders/OpenOrderPage'
import { OrderDetailPage } from './service-orders/OrderDetailPage'
import { MyQueuePage } from './service-orders/MyQueuePage'
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
  // Fora do shell: quem abre este link não tem login (D-15, D-21). O caminho
  // é curto porque vai numa mensagem de WhatsApp.
  { path: '/q/:token', element: <PublicQuotePage /> },
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
      // `new` antes de `:id` para o literal ganhar o casamento de rota.
      {
        path: 'service-orders/new',
        element: (
          <RequireRole allowed={desk}>
            <OpenOrderPage />
          </RequireRole>
        ),
      },
      {
        path: 'service-orders/:id',
        element: <OrderDetailPage />,
      },
      {
        path: 'my-queue',
        element: (
          <RequireRole allowed={['MECHANIC']}>
            <MyQueuePage />
          </RequireRole>
        ),
      },
      {
        path: 'mechanics',
        element: (
          <RequireRole allowed={desk}>
            <Placeholder title="Mecânicos" waitingFor="a tela de cadastro e desempenho" />
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
            <Placeholder title="Veículos" waitingFor="uma tela própria, que o protótipo não prevê" />
          </RequireRole>
        ),
      },
      {
        path: 'quotes',
        element: (
          <RequireRole allowed={desk}>
            <QuotesPage />
          </RequireRole>
        ),
      },
      {
        path: 'inventory',
        element: (
          <RequireRole allowed={desk}>
            <InventoryPage />
          </RequireRole>
        ),
      },
      // `new` antes de `:id`, como em customers: o literal precisa ganhar a rota.
      {
        path: 'inventory/new',
        element: (
          <RequireRole allowed={desk}>
            <PartFormPage />
          </RequireRole>
        ),
      },
      {
        path: 'inventory/:id',
        element: (
          <RequireRole allowed={desk}>
            <PartFormPage />
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
