import { Route, Routes } from 'react-router-dom';
import Header from './components/Header';
import RequireCustomer from './components/RequireCustomer';
import { AuthProvider } from './context/AuthContext';
import { CartProvider } from './context/CartContext';
import Cart from './pages/Cart';
import Checkout from './pages/Checkout';
import Home from './pages/Home';
import Login from './pages/Login';
import NotFound from './pages/NotFound';
import ProductDetail from './pages/ProductDetail';
import Shop from './pages/Shop';
import AccountLayout from './pages/account/AccountLayout';
import Addresses from './pages/account/Addresses';
import Dashboard from './pages/account/Dashboard';
import OrderDetail from './pages/account/OrderDetail';
import Orders from './pages/account/Orders';
import Profile from './pages/account/Profile';

export default function App() {
  return (
    <AuthProvider>
      <CartProvider>
        <div className="min-h-screen pb-24">
          <Header />
          <main>
            <Routes>
              <Route path="/" element={<Home />} />
              <Route path="/shop" element={<Shop />} />
              <Route path="/product/:slug" element={<ProductDetail />} />
              <Route path="/cart" element={<Cart />} />
              <Route path="/login" element={<Login />} />
              <Route path="/checkout" element={<RequireCustomer><Checkout /></RequireCustomer>} />
              <Route path="/account" element={<RequireCustomer><AccountLayout /></RequireCustomer>}>
                <Route index element={<Dashboard />} />
                <Route path="orders" element={<Orders />} />
                <Route path="orders/:id" element={<OrderDetail />} />
                <Route path="addresses" element={<Addresses />} />
                <Route path="profile" element={<Profile />} />
              </Route>
              <Route path="*" element={<NotFound />} />
            </Routes>
          </main>
        </div>
      </CartProvider>
    </AuthProvider>
  );
}
