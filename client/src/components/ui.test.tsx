import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StatusPill, AsyncView } from './ui';

describe('StatusPill', () => {
  it('renders a text label (never colour alone) — 08 §12', () => {
    render(<StatusPill tone="danger" label="Offline" />);
    // The label text must be present so it is distinguishable without colour.
    expect(screen.getByText('Offline')).toBeInTheDocument();
  });

  it('renders an icon glyph alongside the label for non-colour differentiation', () => {
    const { container } = render(<StatusPill tone="success" label="Online" />);
    // An aria-hidden icon glyph accompanies the label.
    expect(container.querySelector('[aria-hidden="true"]')).not.toBeNull();
    expect(screen.getByText('Online')).toBeInTheDocument();
  });
});

describe('AsyncView (five-state handling — 08 §7)', () => {
  it('shows loading state', () => {
    render(<AsyncView isLoading isError={false}><div>data</div></AsyncView>);
    expect(screen.getByText(/loading/i)).toBeInTheDocument();
    expect(screen.queryByText('data')).not.toBeInTheDocument();
  });

  it('shows an alert on error', () => {
    render(<AsyncView isLoading={false} isError><div>data</div></AsyncView>);
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('shows the empty message when empty', () => {
    render(
      <AsyncView isLoading={false} isError={false} isEmpty emptyText="Nothing here">
        <div>data</div>
      </AsyncView>,
    );
    expect(screen.getByText('Nothing here')).toBeInTheDocument();
  });

  it('renders children when loaded, non-empty', () => {
    render(<AsyncView isLoading={false} isError={false}><div>data</div></AsyncView>);
    expect(screen.getByText('data')).toBeInTheDocument();
  });
});
