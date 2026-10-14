import { Link, useNavigate } from 'react-router';
import { Button, Card } from '@/components/ui';

// UI-019 — Unauthorized: the client surface for "nav hides it, but a deep link must still be blocked".
export function UnauthorizedPage() {
  const navigate = useNavigate();
  return (
    <Card className="mx-auto max-w-lg px-8 py-12 text-center">
      <h2 className="text-h2 text-text-primary">Access restricted</h2>
      <p className="mt-3 text-body text-text-secondary">Your current role does not have permission to view this area.</p>
      <div className="mt-6 flex flex-wrap justify-center gap-3">
        <Button variant="outline" onClick={() => navigate(-1)}>Go Back</Button>
        <Link to="/dashboard" className="inline-flex h-10 items-center rounded-md bg-primary px-4 text-body-sm font-semibold text-white hover:bg-primary-hover">Go to Dashboard</Link>
      </div>
    </Card>
  );
}

// UI-020 — Not Found.
export function NotFoundPage() {
  return (
    <main id="main" className="grid min-h-screen place-items-center bg-background px-4">
      <Card className="max-w-lg px-8 py-12 text-center">
        <p className="text-micro-label uppercase text-text-secondary">Error 404</p>
        <h1 className="mt-2 text-h1 text-text-primary">Page not found</h1>
        <p className="mt-3 text-body text-text-secondary">The page you’re looking for doesn’t exist or has been moved.</p>
        <Link to="/" className="mt-6 inline-flex h-10 items-center rounded-md bg-primary px-4 text-body-sm font-semibold text-white hover:bg-primary-hover">Back to Home</Link>
      </Card>
    </main>
  );
}
